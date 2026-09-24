using System.Net;
using System.Security.Claims;
using System.Text;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Models.Requests;
using Bit.Subscriptions.User.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test;

public class UserSubscriptionPurchaseEndpointsRequestBindingTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly UserEntity _user = new() { Id = Guid.NewGuid() };

    public UserSubscriptionPurchaseEndpointsRequestBindingTests() =>
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(_user);

    [Fact]
    public async Task GetPremiumPurchasePreview_BindsQueryParamsIncludingRepeatedCoupons()
    {
        var query = new FakeGetPremiumPurchasePreviewQuery { Result = SamplePreview() };

        var context = await InvokePremiumAsync(query, "additionalStorage=2&coupons=A&coupons=B&country=US&postalCode=12345");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Same(_user, query.ReceivedUser);
        var request = query.ReceivedRequest!;
        Assert.Equal((short)2, request.AdditionalStorage);
        Assert.Equal(new[] { "A", "B" }, request.Coupons);
        Assert.Equal("US", request.Country);
        Assert.Equal("12345", request.PostalCode);
    }

    [Fact]
    public async Task GetPremiumPurchasePreview_WithOnlyTheAddress_BindsNullOptionalParams()
    {
        var query = new FakeGetPremiumPurchasePreviewQuery { Result = SamplePreview() };

        var context = await InvokePremiumAsync(query, "country=US&postalCode=12345");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Null(query.ReceivedRequest!.AdditionalStorage);
        Assert.True(query.ReceivedRequest.Coupons is null or { Length: 0 });
    }

    [Fact]
    public async Task GetPremiumPurchasePreview_WhenQueryThrowsBadRequest_Returns400()
    {
        var query = new FakeGetPremiumPurchasePreviewQuery
        {
            Exception = new Core.Exceptions.BadRequestException("PostalCode", "The PostalCode field is required.")
        };

        var context = await InvokePremiumAsync(query, "country=US");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("\"Families\"")]
    [InlineData("1")]
    public async Task PostOrganizationPurchasePreview_BindsTheTierByNameOrNumber(string tierJson)
    {
        var query = new FakeGetOrganizationPurchasePreviewQuery { Result = SamplePreview() };
        var body = $$"""
            {
              "purchase": {
                "tier": {{tierJson}},
                "cadence": "Annually",
                "passwordManager": { "seats": 1, "additionalStorage": 0, "sponsored": true }
              },
              "billingAddress": { "country": "US", "postalCode": "12345" }
            }
            """;

        var context = await InvokeOrganizationAsync(query, body);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Same(_user, query.ReceivedUser);
        var purchase = query.ReceivedRequest!.Purchase!;
        Assert.Equal(ProductTierType.Families, purchase.Tier);
        Assert.Equal(PlanCadenceType.Annually, purchase.Cadence);
        Assert.True(purchase.PasswordManager!.Sponsored);
        Assert.Null(purchase.SecretsManager);
        Assert.Null(query.ReceivedRequest.BillingAddress!.TaxId);
    }

    [Fact]
    public async Task PostOrganizationPurchasePreview_BindsTheNestedSelectionsAndTaxId()
    {
        var query = new FakeGetOrganizationPurchasePreviewQuery { Result = SamplePreview() };
        const string body = """
            {
              "purchase": {
                "tier": "Enterprise",
                "cadence": "Monthly",
                "passwordManager": { "seats": 12, "additionalStorage": 3, "sponsored": false },
                "secretsManager": { "seats": 5, "additionalServiceAccounts": 10, "standalone": true },
                "coupons": ["A", "B"]
              },
              "billingAddress": {
                "country": "DE",
                "postalCode": "10115",
                "taxId": { "code": "eu_vat", "value": "DE123456789" }
              }
            }
            """;

        var context = await InvokeOrganizationAsync(query, body);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var request = query.ReceivedRequest!;
        var purchase = request.Purchase!;
        Assert.Equal(ProductTierType.Enterprise, purchase.Tier);
        Assert.Equal(PlanCadenceType.Monthly, purchase.Cadence);
        Assert.Equal(new GetOrganizationPurchasePreviewRequest.PasswordManagerSelections(12, 3, false), purchase.PasswordManager);
        Assert.Equal(new GetOrganizationPurchasePreviewRequest.SecretsManagerSelections(5, 10, true), purchase.SecretsManager);
        Assert.Equal(new[] { "A", "B" }, purchase.Coupons);
        Assert.Equal("DE", request.BillingAddress!.Country);
        Assert.Equal("10115", request.BillingAddress.PostalCode);
        Assert.Equal(new GetOrganizationPurchasePreviewRequest.TaxIdSelection("eu_vat", "DE123456789"), request.BillingAddress.TaxId);
    }

    [Fact]
    public async Task PostOrganizationPurchasePreview_WhenQueryThrowsBadRequest_Returns400()
    {
        var query = new FakeGetOrganizationPurchasePreviewQuery
        {
            Exception = new Core.Exceptions.BadRequestException("Purchase", "The Purchase field is required.")
        };

        var context = await InvokeOrganizationAsync(query, "{}");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal(1, query.Calls);
    }

    [Fact]
    public async Task PostOrganizationPurchasePreview_WithAnEmptyBody_Returns400WithoutRunningTheQuery()
    {
        var query = new FakeGetOrganizationPurchasePreviewQuery { Result = SamplePreview() };

        var context = await InvokeOrganizationAsync(query, "");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal(0, query.Calls);
    }

    private Task<HttpContext> InvokeOrganizationAsync(FakeGetOrganizationPurchasePreviewQuery query, string body) =>
        InvokeAsync(
            services =>
            {
                services.AddSingleton<IGetOrganizationPurchasePreviewQuery>(query);
                services.AddScoped<GetAccountOrganizationPurchasePreviewHandler>();
            },
            "GetAccountOrganizationPurchasePreview",
            context =>
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Request.Method = HttpMethods.Post;
                context.Request.ContentType = "application/json";
                context.Request.ContentLength = bytes.Length;
                context.Request.Body = new MemoryStream(bytes);
                // A bare DefaultHttpContext reports no body; the server sets this feature for POST requests.
                context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetection(bytes.Length > 0));
            });

    private Task<HttpContext> InvokePremiumAsync(FakeGetPremiumPurchasePreviewQuery query, string queryString) =>
        InvokeAsync(
            services =>
            {
                services.AddSingleton<IGetPremiumPurchasePreviewQuery>(query);
                services.AddScoped<GetAccountPremiumPurchasePreviewHandler>();
            },
            "GetAccountPremiumPurchasePreview",
            context =>
            {
                context.Request.Method = HttpMethods.Get;
                context.Request.QueryString = new QueryString("?" + queryString);
            });

    private async Task<HttpContext> InvokeAsync(
        Action<IServiceCollection> registerServices, string endpointName, Action<HttpContext> arrangeRequest)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_userService);
        registerServices(builder.Services);
        var app = builder.Build();
        app.MapUserSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == endpointName);

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        arrangeRequest(context);

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Premium,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 19.8m }
        },
        EstimatedTax = 1.76m,
        Total = 21.56m,
        AmountDue = 21.56m
    };

    private sealed class RequestBodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}

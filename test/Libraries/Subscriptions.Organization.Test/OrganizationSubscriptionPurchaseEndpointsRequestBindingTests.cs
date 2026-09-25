using System.Net;
using System.Security.Claims;
using System.Text;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationSubscriptionPurchaseEndpointsRequestBindingTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly UserEntity _user = new() { Id = Guid.NewGuid() };

    public OrganizationSubscriptionPurchaseEndpointsRequestBindingTests() =>
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(_user);

    [Theory]
    [InlineData("\"Families\"", ProductTierType.Families)]
    [InlineData("1", ProductTierType.Families)]
    [InlineData("\"Teams\"", ProductTierType.Teams)]
    [InlineData("2", ProductTierType.Teams)]
    public async Task PreviewOrganizationSubscriptionPurchase_BindsTheTierByNameOrNumber(string tierJson, ProductTierType expectedTier)
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };
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

        var context = await InvokeAsync(query, body);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Same(_user, query.ReceivedUser);
        var purchase = query.ReceivedRequest!.Purchase!;
        Assert.Equal(expectedTier, purchase.Tier);
        Assert.Equal(PlanCadenceType.Annually, purchase.Cadence);
        Assert.True(purchase.PasswordManager!.Sponsored);
        Assert.Null(purchase.SecretsManager);
        Assert.Null(query.ReceivedRequest.BillingAddress!.TaxId);
    }

    [Theory]
    [InlineData("\"annually\"", PlanCadenceType.Annually)]
    [InlineData("\"Monthly\"", PlanCadenceType.Monthly)]
    [InlineData("1", PlanCadenceType.Monthly)]
    public async Task PreviewOrganizationSubscriptionPurchase_BindsTheCadenceByNameOrNumber(string cadenceJson, PlanCadenceType expectedCadence)
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };
        var body = $$"""
            {
              "purchase": {
                "tier": "Teams",
                "cadence": {{cadenceJson}},
                "passwordManager": { "seats": 1, "additionalStorage": 0, "sponsored": false }
              },
              "billingAddress": { "country": "US", "postalCode": "12345" }
            }
            """;

        var context = await InvokeAsync(query, body);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal(expectedCadence, query.ReceivedRequest!.Purchase!.Cadence);
    }

    [Fact]
    public async Task PreviewOrganizationSubscriptionPurchase_WithoutTierOrCadence_BindsThemAsNull()
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };
        const string body = """
            {
              "purchase": { "passwordManager": { "seats": 1, "additionalStorage": 0, "sponsored": false } },
              "billingAddress": { "country": "US", "postalCode": "12345" }
            }
            """;

        await InvokeAsync(query, body);

        Assert.Null(query.ReceivedRequest!.Purchase!.Tier);
        Assert.Null(query.ReceivedRequest.Purchase.Cadence);
    }

    [Theory]
    [InlineData("\"tier\": \"Families\",", "Purchase.Cadence", "The Cadence field is required.")]
    [InlineData("\"cadence\": \"annually\",", "Purchase.Tier", "The Tier field is required.")]
    public async Task PreviewOrganizationSubscriptionPurchase_WithAMissingTierOrCadence_Returns400KeyedOnTheField(
        string presentField, string expectedKey, string expectedMessage)
    {
        var body = $$"""
            {
              "purchase": {
                {{presentField}}
                "passwordManager": { "seats": 1, "additionalStorage": 0, "sponsored": false }
              },
              "billingAddress": { "country": "US", "postalCode": "12345" }
            }
            """;

        var context = await InvokeAsync(
            services => services.AddSingleton<IPreviewOrganizationSubscriptionPurchaseQuery>(RealQuery()), body);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        var responseBody = await ReadBodyAsync(context);
        Assert.Contains($"\"{expectedKey}\"", responseBody);
        Assert.Contains(expectedMessage, responseBody);
    }

    [Fact]
    public async Task PreviewOrganizationSubscriptionPurchase_BindsTheNestedSelectionsAndTaxId()
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };
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

        var context = await InvokeAsync(query, body);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var request = query.ReceivedRequest!;
        var purchase = request.Purchase!;
        Assert.Equal(ProductTierType.Enterprise, purchase.Tier);
        Assert.Equal(PlanCadenceType.Monthly, purchase.Cadence);
        Assert.Equal(new PasswordManagerSelections(12, 3, false), purchase.PasswordManager);
        Assert.Equal(new SecretsManagerSelections(5, 10, true), purchase.SecretsManager);
        Assert.Equal(new[] { "A", "B" }, purchase.Coupons);
        Assert.Equal("DE", request.BillingAddress!.Country);
        Assert.Equal("10115", request.BillingAddress.PostalCode);
        Assert.Equal(new TaxIdSelection("eu_vat", "DE123456789"), request.BillingAddress.TaxId);
    }

    [Fact]
    public async Task PreviewOrganizationSubscriptionPurchase_SetsNoStore()
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };
        const string body = """
            {
              "purchase": {
                "tier": "Teams",
                "cadence": "Annually",
                "passwordManager": { "seats": 1, "additionalStorage": 0, "sponsored": false }
              },
              "billingAddress": { "country": "US", "postalCode": "12345" }
            }
            """;

        var context = await InvokeAsync(query, body);

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task PreviewOrganizationSubscriptionPurchase_WhenQueryThrowsBadRequest_Returns400()
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery
        {
            Exception = new Core.Exceptions.BadRequestException("Purchase", "The Purchase field is required.")
        };

        var context = await InvokeAsync(query, "{}");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal(1, query.Calls);
    }

    [Fact]
    public async Task PreviewOrganizationSubscriptionPurchase_WithAnEmptyBody_Returns400WithoutRunningTheQuery()
    {
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = SamplePreview() };

        var context = await InvokeAsync(query, "");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal(0, query.Calls);
    }

    private Task<HttpContext> InvokeAsync(FakePreviewOrganizationSubscriptionPurchaseQuery query, string body) =>
        InvokeAsync(services => services.AddSingleton<IPreviewOrganizationSubscriptionPurchaseQuery>(query), body);

    private async Task<HttpContext> InvokeAsync(Action<IServiceCollection> registerQuery, string body)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_userService);
        registerQuery(builder.Services);
        builder.Services.AddScoped<PreviewOrganizationSubscriptionPurchaseHandler>();
        var app = builder.Build();
        app.MapOrganizationSubscriptionPurchaseEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "PreviewOrganizationSubscriptionPurchase");

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();

        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        // A bare DefaultHttpContext reports no body; the server sets this feature for POST requests.
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetection(bytes.Length > 0));

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static PreviewOrganizationSubscriptionPurchaseQuery RealQuery() => new(
        new RecordingLogger<PreviewOrganizationSubscriptionPurchaseQuery>(),
        Substitute.For<IPricingClient>(),
        Substitute.For<ISubscriptionDiscountService>(),
        Substitute.For<ITaxService>(),
        Substitute.For<IInvoicePreviewService>());

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Families,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 40m }
        },
        EstimatedTax = 3.55m,
        Total = 43.55m,
        AmountDue = 43.55m
    };

    private sealed class RequestBodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}

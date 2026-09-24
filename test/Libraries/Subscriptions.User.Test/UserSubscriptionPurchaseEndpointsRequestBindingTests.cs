using System.Net;
using System.Security.Claims;
using System.Text;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
            .Single(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName == endpointName);

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
}

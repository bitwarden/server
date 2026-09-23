using System.Net;
using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
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

public class UserSubscriptionEndpointsRequestBindingTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();

    [Fact]
    public async Task GetUpgradePreview_BindsQueryParamsAndReturnsThePreview()
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        var query = new FakeGetSubscriptionUpgradePreviewQuery { Result = SamplePreview() };

        var context = await InvokeAsync(query, "targetProductTierType=2&country=US&postalCode=12345");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Same(user, query.ReceivedUser);
        Assert.Equal(ProductTierType.Teams, query.ReceivedRequest!.TargetProductTierType);
        Assert.Equal("US", query.ReceivedRequest.Country);
        Assert.Equal("12345", query.ReceivedRequest.PostalCode);
    }

    [Fact]
    public async Task GetUpgradePreview_BindsTheEnumTierFromQuery()
    {
        // The client sends the numeric tier, but the query binder also accepts the exact enum name;
        // pin that so a future binding change can't silently break the tier parameter.
        var user = new UserEntity { Id = Guid.NewGuid() };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        var query = new FakeGetSubscriptionUpgradePreviewQuery { Result = SamplePreview() };

        var context = await InvokeAsync(query, "targetProductTierType=Teams&country=US&postalCode=12345");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal(ProductTierType.Teams, query.ReceivedRequest!.TargetProductTierType);
    }

    [Fact]
    public async Task GetUpgradePreview_WhenQueryThrowsBadRequest_Returns400WithModelState()
    {
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(new UserEntity { Id = Guid.NewGuid() });
        var query = new FakeGetSubscriptionUpgradePreviewQuery
        {
            Exception = new Core.Exceptions.BadRequestException("PostalCode", "The PostalCode field is required.")
        };

        var context = await InvokeAsync(query, "targetProductTierType=2&country=US");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
    }

    private async Task<HttpContext> InvokeAsync(FakeGetSubscriptionUpgradePreviewQuery query, string queryString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_userService);
        builder.Services.AddSingleton<IGetSubscriptionUpgradePreviewQuery>(query);
        builder.Services.AddSingleton(Substitute.For<IGetSubscriptionPreviewQuery>());
        builder.Services.AddScoped<UserSubscriptionEndpointsHandler>();
        var app = builder.Build();
        app.MapUserSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("upgrade/preview", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString("?" + queryString);
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Teams,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Prorations = [new PurchasableProration { Reference = "pm-seat", Charge = 26.67m, Credit = 6.67m, Tax = 2m, Total = 20m, Months = 8 }],
        },
        EstimatedTax = 2m,
        Total = 22m,
        AmountDue = 22m
    };
}

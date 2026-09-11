using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Commands;
using Bit.Subscriptions.User.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public async Task PreviewPremiumUpgrade_BindsTheInternalRequestFromJsonAndReturnsThePreview()
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        var command = new FakePreviewPremiumUpgradeCommand { Result = SamplePreview() };

        var context = await InvokeAsync(command, """{"targetProductTierType":2,"billingAddress":{"country":"US","postalCode":"12345"}}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Same(user, command.ReceivedUser);
        Assert.Equal(ProductTierType.Teams, command.ReceivedRequest!.TargetProductTierType);
        Assert.Equal("US", command.ReceivedRequest.BillingAddress.Country);
        Assert.Equal("12345", command.ReceivedRequest.BillingAddress.PostalCode);

        using var body = JsonDocument.Parse(ReadBody(context));
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("passwordManager").GetProperty("seats").ValueKind);
        Assert.Equal(26.67m, body.RootElement.GetProperty("passwordManager").GetProperty("prorations")[0].GetProperty("charge").GetDecimal());
    }

    [Fact]
    public async Task PreviewPremiumUpgrade_WhenCommandThrowsBadRequest_Returns400WithModelState()
    {
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(new UserEntity { Id = Guid.NewGuid() });
        var command = new FakePreviewPremiumUpgradeCommand
        {
            Exception = new Core.Exceptions.BadRequestException("BillingAddress", "The BillingAddress field is required.")
        };

        var context = await InvokeAsync(command, """{"targetProductTierType":2,"billingAddress":null}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Contains("BillingAddress", ReadBody(context));
    }

    private async Task<HttpContext> InvokeAsync(FakePreviewPremiumUpgradeCommand command, string json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_userService);
        builder.Services.AddSingleton<IPreviewPremiumUpgradeCommand>(command);
        builder.Services.AddScoped<UserSubscriptionEndpointsHandler>();
        var app = builder.Build();
        app.MapUserSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("upgrade/invoice/preview", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Post;
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new HasBody());
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private sealed class HasBody : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Teams,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Prorations = [new PurchasableProration { Charge = 26.67m, Credit = 6.67m, Tax = 2m, Total = 20m, Months = 8 }]
        },
        EstimatedTax = 2m,
        Total = 22m,
        AmountDue = 22m
    };
}

using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Models.Requests;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test.Handlers;

public class GetAccountSubscriptionUpgradePreviewHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly ClaimsPrincipal _principal = new();

    [Fact]
    public async Task HandleAsync_WhenPrincipalDoesNotResolveToUser_ThrowsUnauthorized()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns((UserEntity?)null);
        var query = new FakeGetSubscriptionUpgradePreviewQuery();
        var sut = new GetAccountSubscriptionUpgradePreviewHandler(_userService, query);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.HandleAsync(_principal, Request()));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public async Task HandleAsync_RunsTheQueryForTheResolvedUser()
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        var request = Request();
        var preview = SamplePreview();
        _userService.GetUserByPrincipalAsync(_principal).Returns(user);
        var query = new FakeGetSubscriptionUpgradePreviewQuery { Result = preview };
        var sut = new GetAccountSubscriptionUpgradePreviewHandler(_userService, query);

        var result = await sut.HandleAsync(_principal, request);

        Assert.Same(preview, result);
        Assert.Same(user, query.ReceivedUser);
        Assert.Same(request, query.ReceivedRequest);
    }

    private static GetSubscriptionUpgradePreviewRequest Request() =>
        new(ProductTierType.Teams, "US", "12345");

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

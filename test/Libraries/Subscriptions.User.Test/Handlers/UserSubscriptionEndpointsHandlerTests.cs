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

public class UserSubscriptionEndpointsHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly ClaimsPrincipal _principal = new();

    [Fact]
    public async Task PreviewPremiumUpgrade_WhenPrincipalDoesNotResolveToUser_ThrowsUnauthorized()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns((UserEntity?)null);
        var command = new FakePreviewPremiumUpgradeCommand();
        var sut = new UserSubscriptionEndpointsHandler(_userService, command);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.PreviewPremiumUpgradeAsync(_principal, Request()));
        Assert.Equal(0, command.Calls);
    }

    [Fact]
    public async Task PreviewPremiumUpgrade_RunsTheCommandForTheResolvedUser()
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        var request = Request();
        var preview = SamplePreview();
        _userService.GetUserByPrincipalAsync(_principal).Returns(user);
        var command = new FakePreviewPremiumUpgradeCommand { Result = preview };
        var sut = new UserSubscriptionEndpointsHandler(_userService, command);

        var result = await sut.PreviewPremiumUpgradeAsync(_principal, request);

        Assert.Same(preview, result);
        Assert.Same(user, command.ReceivedUser);
        Assert.Same(request, command.ReceivedRequest);
    }

    private static PreviewPremiumUpgradeRequest Request() => new()
    {
        TargetProductTierType = ProductTierType.Teams,
        BillingAddress = new BillingAddressRequest { Country = "US", PostalCode = "12345" }
    };

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

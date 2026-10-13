using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class PreviewOrganizationSubscriptionPurchaseHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly ClaimsPrincipal _principal = new();

    [Fact]
    public async Task HandleAsync_WhenPrincipalDoesNotResolveToUser_ThrowsUnauthorized()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns((UserEntity?)null);
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery();
        var sut = new PreviewOrganizationSubscriptionPurchaseHandler(_userService, query);

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
        var query = new FakePreviewOrganizationSubscriptionPurchaseQuery { Result = preview };
        var sut = new PreviewOrganizationSubscriptionPurchaseHandler(_userService, query);

        var result = await sut.HandleAsync(_principal, request);

        Assert.Same(preview, result);
        Assert.Same(user, query.ReceivedUser);
        Assert.Same(request, query.ReceivedRequest);
    }

    private static PreviewOrganizationSubscriptionPurchaseRequest Request() => new(
        new PurchaseSelections(ProductTierType.Families, PlanCadenceType.Annually, new PasswordManagerSelections(1, 0, false), null, null),
        new BillingAddressSelections("US", "12345", null));

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
}

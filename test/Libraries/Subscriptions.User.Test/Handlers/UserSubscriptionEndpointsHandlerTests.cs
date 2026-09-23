using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Models.Requests;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test.Handlers;

public class UserSubscriptionEndpointsHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IGetSubscriptionPreviewQuery _getSubscriptionPreviewQuery =
        Substitute.For<IGetSubscriptionPreviewQuery>();
    private readonly ClaimsPrincipal _principal = new();
    private readonly UserSubscriptionEndpointsHandler _sut;

    public UserSubscriptionEndpointsHandlerTests() =>
        _sut = new UserSubscriptionEndpointsHandler(_userService, new FakeGetSubscriptionUpgradePreviewQuery(), _getSubscriptionPreviewQuery);

    [Fact]
    public async Task GetUpgradePreviewAsync_WhenPrincipalDoesNotResolveToUser_ThrowsUnauthorized()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns((UserEntity?)null);
        var query = new FakeGetSubscriptionUpgradePreviewQuery();
        var sut = new UserSubscriptionEndpointsHandler(_userService, query, _getSubscriptionPreviewQuery);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.GetUpgradePreviewAsync(_principal, Request()));
        Assert.Equal(0, query.Calls);
    }

    [Fact]
    public async Task GetUpgradePreviewAsync_RunsTheQueryForTheResolvedUser()
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        var request = Request();
        var preview = SamplePreview();
        _userService.GetUserByPrincipalAsync(_principal).Returns(user);
        var query = new FakeGetSubscriptionUpgradePreviewQuery { Result = preview };
        var sut = new UserSubscriptionEndpointsHandler(_userService, query, _getSubscriptionPreviewQuery);

        var result = await sut.GetUpgradePreviewAsync(_principal, request);

        Assert.Same(preview, result);
        Assert.Same(user, query.ReceivedUser);
        Assert.Same(request, query.ReceivedRequest);
    }

    [Fact]
    public async Task GetPreview_WhenUserCannotBeResolved_ThrowsNotFound()
    {
        var principal = new ClaimsPrincipal();
        _userService.GetUserByPrincipalAsync(principal).Returns((UserEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetPreviewAsync(principal));
    }

    [Fact]
    public async Task GetPreview_WhenPreviewNull_ThrowsNotFound()
    {
        var principal = new ClaimsPrincipal();
        var user = new UserEntity { Id = Guid.NewGuid() };
        _userService.GetUserByPrincipalAsync(principal).Returns(user);
        _getSubscriptionPreviewQuery.Run(user).Returns((SubscriptionPreview?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetPreviewAsync(principal));
    }

    [Fact]
    public async Task GetPreview_ReturnsPreviewFromQuery()
    {
        var principal = new ClaimsPrincipal();
        var user = new UserEntity { Id = Guid.NewGuid() };
        var preview = SampleSubscriptionPreview();
        _userService.GetUserByPrincipalAsync(principal).Returns(user);
        _getSubscriptionPreviewQuery.Run(user).Returns(preview);

        var result = await _sut.GetPreviewAsync(principal);

        Assert.Same(preview, result);
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

    private static SubscriptionPreview SampleSubscriptionPreview() => new()
    {
        Status = "active",
        InvoicePreview = new InvoicePreview
        {
            PasswordManager = new PasswordManagerInvoiceItems
            {
                Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 10m }
            },
            Cadence = PlanCadenceType.Annually,
            PlanTier = PlanTierType.Premium,
            EstimatedTax = 0m,
            Total = 10m,
            AmountDue = 10m
        }
    };
}

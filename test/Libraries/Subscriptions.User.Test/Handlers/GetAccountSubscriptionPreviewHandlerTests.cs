using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Invoicing.InvoicePreviews.Queries;
using Bit.Subscriptions.User.Handlers;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test.Handlers;

public class GetAccountSubscriptionPreviewHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IGetSubscriptionPreviewQuery _getSubscriptionPreviewQuery =
        Substitute.For<IGetSubscriptionPreviewQuery>();
    private readonly GetAccountSubscriptionPreviewHandler _sut;

    public GetAccountSubscriptionPreviewHandlerTests() =>
        _sut = new GetAccountSubscriptionPreviewHandler(_userService, _getSubscriptionPreviewQuery);

    [Fact]
    public async Task HandleAsync_WhenUserCannotBeResolved_ThrowsNotFound()
    {
        var principal = new ClaimsPrincipal();
        _userService.GetUserByPrincipalAsync(principal).Returns((UserEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(principal));
    }

    [Fact]
    public async Task HandleAsync_WhenPreviewNull_ThrowsNotFound()
    {
        var principal = new ClaimsPrincipal();
        var user = new UserEntity { Id = Guid.NewGuid() };
        _userService.GetUserByPrincipalAsync(principal).Returns(user);
        _getSubscriptionPreviewQuery.Run(user).Returns((SubscriptionPreview?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(principal));
    }

    [Fact]
    public async Task HandleAsync_ReturnsPreviewFromQuery()
    {
        var principal = new ClaimsPrincipal();
        var user = new UserEntity { Id = Guid.NewGuid() };
        var preview = SampleSubscriptionPreview();
        _userService.GetUserByPrincipalAsync(principal).Returns(user);
        _getSubscriptionPreviewQuery.Run(user).Returns(preview);

        var result = await _sut.HandleAsync(principal);

        Assert.Same(preview, result);
    }

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

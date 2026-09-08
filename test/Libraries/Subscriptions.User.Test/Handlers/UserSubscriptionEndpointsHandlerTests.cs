using System.Security.Claims;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Invoicing.InvoicePreviews.Commands;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Models.Requests;
using NSubstitute;
using Xunit;
using UserEntity = Bit.Core.Entities.User;

namespace Bit.Subscriptions.User.Test;

public class UserSubscriptionEndpointsHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();

    private readonly IBuildInvoicePreviewForPremiumOrgUpgradeCommand _buildInvoicePreviewForPremiumOrgUpgradeCommand =
        Substitute.For<IBuildInvoicePreviewForPremiumOrgUpgradeCommand>();

    private readonly UserSubscriptionEndpointsHandler _sut;
    private readonly ClaimsPrincipal _principal = new();

    public UserSubscriptionEndpointsHandlerTests() =>
        _sut = new UserSubscriptionEndpointsHandler(_userService, _buildInvoicePreviewForPremiumOrgUpgradeCommand);

    [Fact]
    public async Task PreviewPremiumOrgUpgrade_WhenPrincipalDoesNotResolveToUser_ThrowsUnauthorized()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns((UserEntity?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.PreviewPremiumOrgUpgradeAsync(_principal, Request(ProductTierType.Families)));
    }

    [Theory]
    [InlineData(ProductTierType.Families, PlanType.FamiliesAnnually)]
    [InlineData(ProductTierType.Teams, PlanType.TeamsAnnually)]
    [InlineData(ProductTierType.Enterprise, PlanType.EnterpriseAnnually)]
    public async Task PreviewPremiumOrgUpgrade_PassesMappedPlanTypeAndAddressToCommand(
        ProductTierType targetProductTierType, PlanType expectedPlanType)
    {
        var user = new UserEntity { Id = Guid.NewGuid() };
        var preview = SamplePreview();
        _userService.GetUserByPrincipalAsync(_principal).Returns(user);
        _buildInvoicePreviewForPremiumOrgUpgradeCommand
            .Run(user, expectedPlanType, Arg.Any<BillingAddress>())
            .Returns(preview);

        var result = await _sut.PreviewPremiumOrgUpgradeAsync(_principal, Request(targetProductTierType));

        Assert.Same(preview, result);
        await _buildInvoicePreviewForPremiumOrgUpgradeCommand.Received(1).Run(
            user,
            expectedPlanType,
            Arg.Is<BillingAddress>(address => address.Country == "US" && address.PostalCode == "12345"));
    }

    /// <summary>The tier is caller input, so an unsupported value is a 400, not a 500.</summary>
    [Fact]
    public async Task PreviewPremiumOrgUpgrade_WhenTargetTierIsNotUpgradable_ThrowsBadRequest()
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns(new UserEntity { Id = Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PreviewPremiumOrgUpgradeAsync(_principal, Request(ProductTierType.Free)));

        Assert.NotNull(exception.ModelState);
        Assert.True(exception.ModelState!.ContainsKey(nameof(PreviewInvoiceForPremiumOrgUpgradeRequest.TargetProductTierType)));
    }

    [Theory]
    [InlineData("", "12345", nameof(PreviewInvoiceBillingAddressRequest.Country))]
    [InlineData("  ", "12345", nameof(PreviewInvoiceBillingAddressRequest.Country))]
    [InlineData("USA", "12345", nameof(PreviewInvoiceBillingAddressRequest.Country))]
    [InlineData("US", "", nameof(PreviewInvoiceBillingAddressRequest.PostalCode))]
    [InlineData("US", "   ", nameof(PreviewInvoiceBillingAddressRequest.PostalCode))]
    public async Task PreviewPremiumOrgUpgrade_WhenBillingAddressIsMalformed_ThrowsBadRequest(
        string country, string postalCode, string expectedKey)
    {
        _userService.GetUserByPrincipalAsync(_principal).Returns(new UserEntity { Id = Guid.NewGuid() });
        var request = new PreviewInvoiceForPremiumOrgUpgradeRequest
        {
            TargetProductTierType = ProductTierType.Teams,
            BillingAddress = new PreviewInvoiceBillingAddressRequest { Country = country, PostalCode = postalCode }
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PreviewPremiumOrgUpgradeAsync(_principal, request));

        Assert.NotNull(exception.ModelState);
        Assert.True(exception.ModelState!.ContainsKey(expectedKey));
        await _buildInvoicePreviewForPremiumOrgUpgradeCommand.DidNotReceiveWithAnyArgs()
            .Run(default!, default, default!);
    }

    private static PreviewInvoiceForPremiumOrgUpgradeRequest Request(ProductTierType targetProductTierType) => new()
    {
        TargetProductTierType = targetProductTierType,
        BillingAddress = new PreviewInvoiceBillingAddressRequest { Country = "US", PostalCode = "12345" }
    };

    private static InvoicePreview SamplePreview() => new()
    {
        PlanTier = PlanTierType.Families,
        Cadence = PlanCadenceType.Annually,
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 26.67m }
        },
        EstimatedTax = 2m,
        Total = 22m,
        AmountDue = 22m
    };
}

using Bit.Api.Billing.Controllers.VNext;
using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class AccountBillingVNextControllerTests
{
    private readonly ICreateBitPayInvoiceForCreditCommand _createBitPayInvoiceForCreditCommand;
    private readonly ICreatePremiumCloudHostedSubscriptionCommand _createPremiumCloudHostedSubscriptionCommand;
    private readonly IGetCreditQuery _getCreditQuery;
    private readonly IGetPaymentMethodQuery _getPaymentMethodQuery;
    private readonly IGetUserLicenseQuery _getUserLicenseQuery;
    private readonly IUpdatePaymentMethodCommand _updatePaymentMethodCommand;
    private readonly IUpgradePremiumToOrganizationCommand _upgradePremiumToOrganizationCommand;
    private readonly AccountBillingVNextController _sut;

    public AccountBillingVNextControllerTests()
    {
        _createBitPayInvoiceForCreditCommand = Substitute.For<ICreateBitPayInvoiceForCreditCommand>();
        _createPremiumCloudHostedSubscriptionCommand = Substitute.For<ICreatePremiumCloudHostedSubscriptionCommand>();
        _getCreditQuery = Substitute.For<IGetCreditQuery>();
        _getPaymentMethodQuery = Substitute.For<IGetPaymentMethodQuery>();
        _getUserLicenseQuery = Substitute.For<IGetUserLicenseQuery>();
        _updatePaymentMethodCommand = Substitute.For<IUpdatePaymentMethodCommand>();
        _upgradePremiumToOrganizationCommand = Substitute.For<IUpgradePremiumToOrganizationCommand>();

        _sut = new AccountBillingVNextController(
            _createBitPayInvoiceForCreditCommand,
            _createPremiumCloudHostedSubscriptionCommand,
            _getCreditQuery,
            _getPaymentMethodQuery,
            _getUserLicenseQuery,
            _updatePaymentMethodCommand,
            _upgradePremiumToOrganizationCommand);
    }

    [Theory, BitAutoData]
    public async Task GetLicenseAsync_ValidUser_ReturnsLicenseResponse(User user,
        Core.Billing.Licenses.Models.Api.Response.LicenseResponseModel licenseResponse)
    {
        // Arrange
        _getUserLicenseQuery.Run(user).Returns(licenseResponse);

        // Act
        var result = await _sut.GetLicenseAsync(user);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _getUserLicenseQuery.Received(1).Run(user);
    }

}

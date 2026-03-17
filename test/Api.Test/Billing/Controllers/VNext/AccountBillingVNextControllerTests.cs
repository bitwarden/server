using Bit.Api.Billing.Controllers.VNext;
using Bit.Api.Billing.Models.Requests.Storage;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Models.Api.Response;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Portal.Commands;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using OneOf.Types;
using Stripe;
using Xunit;
using BadRequest = Bit.Core.Billing.Commands.BadRequest;
using Conflict = Bit.Core.Billing.Commands.Conflict;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class AccountBillingVNextControllerTests
{
    private readonly IUpdatePremiumStorageCommand _updatePremiumStorageCommand;
    private readonly IGetUserLicenseQuery _getUserLicenseQuery;
    private readonly IUpgradePremiumToOrganizationCommand _upgradePremiumToOrganizationCommand;
    private readonly IGetApplicableDiscountsQuery _getApplicableDiscountsQuery;
    private readonly ICreateBillingPortalSessionCommand _createBillingPortalSessionCommand;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly AccountBillingVNextController _sut;

    public AccountBillingVNextControllerTests()
    {
        _updatePremiumStorageCommand = Substitute.For<IUpdatePremiumStorageCommand>();
        _getUserLicenseQuery = Substitute.For<IGetUserLicenseQuery>();
        _upgradePremiumToOrganizationCommand = Substitute.For<IUpgradePremiumToOrganizationCommand>();
        _getApplicableDiscountsQuery = Substitute.For<IGetApplicableDiscountsQuery>();
        _createBillingPortalSessionCommand = Substitute.For<ICreateBillingPortalSessionCommand>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = new GlobalSettings
        {
            BaseServiceUri = new GlobalSettings.BaseServiceUriSettings(new GlobalSettings())
        };
        _globalSettings.BaseServiceUri.Vault = "https://vault.bitwarden.com";

        _sut = new AccountBillingVNextController(
            _createBillingPortalSessionCommand,
            Substitute.For<Core.Billing.Payment.Commands.ICreateBitPayInvoiceForCreditCommand>(),
            Substitute.For<Core.Billing.Premium.Commands.ICreatePremiumCloudHostedSubscriptionCommand>(),
            _currentContext,
            _getApplicableDiscountsQuery,
            Substitute.For<IGetBitwardenSubscriptionQuery>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetCreditQuery>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetPaymentMethodQuery>(),
            _getUserLicenseQuery,
            _globalSettings,
            Substitute.For<IReinstateSubscriptionCommand>(),
            Substitute.For<Core.Billing.Payment.Commands.IUpdatePaymentMethodCommand>(),
            _updatePremiumStorageCommand,
            _upgradePremiumToOrganizationCommand);
    }

    [Theory, BitAutoData]
    public async Task GetLicenseAsync_ValidUser_ReturnsLicenseResponse(
        User user,
        UserLicense license)
    {
        // Arrange
        _getUserLicenseQuery.Run(user).Returns(license);
        // Act
        var result = await _sut.GetLicenseAsync(user);
        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _getUserLicenseQuery.Received(1).Run(user);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_Success_ReturnsOk(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 10 };

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(new BillingCommandResult<None>(new None()));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 10 };
        var errorMessage = "User does not have a premium subscription.";

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_NoPaymentMethod_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 10 };
        var errorMessage = "No payment method found.";

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageLessThanBase_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 1 };
        var errorMessage = "Storage cannot be less than the base amount of 1 GB.";

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 1))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 1);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageExceedsMaximum_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 100 };
        var errorMessage = "Maximum storage is 100 GB.";

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 100))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 100);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageExceedsCurrentUsage_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 2 };
        var errorMessage = "You are currently using 5.00 GB of storage. Delete some stored data first.";

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 2))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 2);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_IncreaseStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 15 };

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 15))
            .Returns(new BillingCommandResult<None>(new None()));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 15);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_DecreaseStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 3 };

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 3))
            .Returns(new BillingCommandResult<None>(new None()));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 3);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_MaximumStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 100 };

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 100))
            .Returns(new BillingCommandResult<None>(new None()));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 100);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_NullPaymentSecret_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { AdditionalStorageGb = 5 };

        _updatePremiumStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 5))
            .Returns(new BillingCommandResult<None>(new None()));

        // Act
        var result = await _sut.UpdateSubscriptionStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updatePremiumStorageCommand.Received(1).Run(user, 5);
    }

    [Theory, BitAutoData]
    public async Task GetApplicableDiscountsAsync_NoEligibleDiscounts_ReturnsOkWithEmptyArray(User user)
    {
        // Arrange
        _getApplicableDiscountsQuery.Run(user)
            .Returns(Array.Empty<SubscriptionDiscountResponseModel>());

        // Act
        var result = await _sut.GetApplicableDiscountsAsync(user);

        // Assert
        var okResult = Assert.IsType<Ok<SubscriptionDiscountResponseModel[]>>(result);
        Assert.Empty(okResult.Value!);
        await _getApplicableDiscountsQuery.Received(1).Run(user);
    }

    [Theory, BitAutoData]
    public async Task GetApplicableDiscountsAsync_EligibleDiscounts_ReturnsOkWithDiscounts(
        User user,
        SubscriptionDiscountResponseModel firstModel,
        SubscriptionDiscountResponseModel secondModel)
    {
        // Arrange
        var models = new[] { firstModel, secondModel };
        _getApplicableDiscountsQuery.Run(user).Returns(models);

        // Act
        var result = await _sut.GetApplicableDiscountsAsync(user);

        // Assert
        var okResult = Assert.IsType<Ok<SubscriptionDiscountResponseModel[]>>(result);
        Assert.Equal(models, okResult.Value);
        await _getApplicableDiscountsQuery.Received(1).Run(user);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_Success_ReturnsPortalUrlAsync(User user)
    {
        // Arrange
        var portalUrl = "https://billing.stripe.com/session/test123";
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";

        _currentContext.DeviceType.Returns(DeviceType.ChromeBrowser);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(portalUrl));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_NoCustomerId_ReturnsBadRequestAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "bitwarden://premium-upgrade-callback";

        _currentContext.DeviceType.Returns(DeviceType.Android);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new BadRequest("User does not have a Stripe customer ID.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_NoSubscriptionId_ReturnsBadRequestAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "bitwarden://premium-upgrade-callback";

        _currentContext.DeviceType.Returns(DeviceType.iOS);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new BadRequest("User does not have a Premium subscription.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_InvalidSubscriptionStatus_ReturnsBadRequestAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";

        _currentContext.DeviceType.Returns((DeviceType?)null);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new BadRequest("Your subscription cannot be managed in its current status.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_SubscriptionNotFound_ReturnsBadRequestAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";

        _currentContext.DeviceType.Returns(DeviceType.WindowsDesktop);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new BadRequest("User subscription not found.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_StripeException_ReturnsServerErrorAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";
        var exception = new StripeException("Stripe API error");

        _currentContext.DeviceType.Returns(DeviceType.MacOsDesktop);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new Unhandled(exception)));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_SessionWithNullUrl_ReturnsServerErrorAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";

        _currentContext.DeviceType.Returns(DeviceType.ChromeExtension);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new Conflict("Unable to create billing portal session. Please contact support for assistance.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }

    [Theory, BitAutoData]
    public async Task CreatePortalSessionAsync_NullSession_ReturnsServerErrorAsync(User user)
    {
        // Arrange
        var expectedReturnUrl = "https://vault.bitwarden.com/#/settings/subscription/premium";

        _currentContext.DeviceType.Returns(DeviceType.LinuxDesktop);
        _createBillingPortalSessionCommand.Run(user, expectedReturnUrl)
            .Returns(new BillingCommandResult<string>(new Conflict("Unable to create billing portal session. Please contact support for assistance.")));

        // Act
        var result = await _sut.CreatePortalSessionAsync(user);

        // Assert
        Assert.IsAssignableFrom<IResult>(result);
        await _createBillingPortalSessionCommand.Received(1).Run(user, expectedReturnUrl);
    }
}

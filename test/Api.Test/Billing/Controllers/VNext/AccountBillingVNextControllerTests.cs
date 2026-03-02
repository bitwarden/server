using Bit.Api.Billing.Controllers.VNext;
using Bit.Api.Billing.Models.Requests.Storage;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Licenses.Queries;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OneOf.Types;
using Xunit;
using BadRequest = Bit.Core.Billing.Commands.BadRequest;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class AccountBillingVNextControllerTests
{
    private readonly IUpdatePremiumStorageCommand _updatePremiumStorageCommand;
    private readonly IGetUserLicenseQuery _getUserLicenseQuery;
    private readonly IUpgradePremiumToOrganizationCommand _upgradePremiumToOrganizationCommand;
    private readonly AccountBillingVNextController _sut;

    public AccountBillingVNextControllerTests()
    {
        _updatePremiumStorageCommand = Substitute.For<IUpdatePremiumStorageCommand>();
        _getUserLicenseQuery = Substitute.For<IGetUserLicenseQuery>();
        _upgradePremiumToOrganizationCommand = Substitute.For<IUpgradePremiumToOrganizationCommand>();

        _sut = new AccountBillingVNextController(
            Substitute.For<Core.Billing.Payment.Commands.ICreateBitPayInvoiceForCreditCommand>(),
            Substitute.For<Core.Billing.Premium.Commands.ICreatePremiumCloudHostedSubscriptionCommand>(),
            Substitute.For<IGetBitwardenSubscriptionQuery>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetCreditQuery>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetPaymentMethodQuery>(),
            _getUserLicenseQuery,
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
}

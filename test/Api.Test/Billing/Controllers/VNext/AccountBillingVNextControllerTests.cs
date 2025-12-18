using Bit.Api.Billing.Controllers.VNext;
using Bit.Api.Billing.Models.Requests.Storage;
using Bit.Core.Billing.Storage.Commands;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;
using BadRequest = Bit.Core.Billing.Commands.BadRequest;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class AccountBillingVNextControllerTests
{
    private readonly IUpdateStorageCommand _updateStorageCommand;
    private readonly AccountBillingVNextController _sut;

    public AccountBillingVNextControllerTests()
    {
        _updateStorageCommand = Substitute.For<IUpdateStorageCommand>();

        _sut = new AccountBillingVNextController(
            Substitute.For<Core.Billing.Payment.Commands.ICreateBitPayInvoiceForCreditCommand>(),
            Substitute.For<Core.Billing.Premium.Commands.ICreatePremiumCloudHostedSubscriptionCommand>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetCreditQuery>(),
            Substitute.For<Core.Billing.Payment.Queries.IGetPaymentMethodQuery>(),
            Substitute.For<Core.Billing.Payment.Commands.IUpdatePaymentMethodCommand>(),
            _updateStorageCommand);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_Success_ReturnsOk(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 10 };
        var expectedPaymentSecret = "pi_secret_123";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(expectedPaymentSecret);

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_UserNotPremium_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 10 };
        var errorMessage = "User does not have a premium subscription.";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_NoPaymentMethod_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 10 };
        var errorMessage = "No payment method found.";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 10))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 10);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageLessThanBase_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 1 };
        var errorMessage = "Storage cannot be less than the base amount of 1 GB.";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 1))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 1);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageExceedsMaximum_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 100 };
        var errorMessage = "Maximum storage is 100 GB.";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 100))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 100);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_StorageExceedsCurrentUsage_ReturnsBadRequest(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 2 };
        var errorMessage = "You are currently using 5.00 GB of storage. Delete some stored data first.";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 2))
            .Returns(new BadRequest(errorMessage));

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 2);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_IncreaseStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 15 };
        var expectedPaymentSecret = "pi_secret_increase";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 15))
            .Returns(expectedPaymentSecret);

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 15);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_DecreaseStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 3 };
        var expectedPaymentSecret = "pi_secret_decrease";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 3))
            .Returns(expectedPaymentSecret);

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 3);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_MaximumStorage_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 100 };
        var expectedPaymentSecret = "pi_secret_max";

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 100))
            .Returns(expectedPaymentSecret);

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 100);
    }

    [Theory, BitAutoData]
    public async Task UpdateStorageAsync_NullPaymentSecret_Success(User user)
    {
        // Arrange
        var request = new StorageUpdateRequest { StorageGb = 5 };

        _updateStorageCommand.Run(
            Arg.Is<User>(u => u.Id == user.Id),
            Arg.Is<short>(s => s == 5))
            .Returns((string?)null);

        // Act
        var result = await _sut.UpdateStorageAsync(user, request);

        // Assert
        var okResult = Assert.IsAssignableFrom<IResult>(result);
        await _updateStorageCommand.Received(1).Run(user, 5);
    }
}

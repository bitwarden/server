using System.Runtime.CompilerServices;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class DeviceServiceTests
{
    [Fact]
    public async Task DeviceSaveShouldUpdateRevisionDateAndPushRegistration()
    {
        var deviceRepo = Substitute.For<IDeviceRepository>();
        var pushRepo = Substitute.For<IPushRegistrationService>();
        var deviceService = new DeviceService(deviceRepo, pushRepo);

        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var device = new Device
        {
            Id = id,
            Name = "test device",
            Type = DeviceType.Android,
            UserId = userId,
            PushToken = "testtoken",
            Identifier = "testid"
        };
        await deviceService.SaveAsync(device);

        Assert.True(device.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
        await pushRepo.Received().CreateOrUpdateRegistrationAsync("testtoken", id.ToString(),
            userId.ToString(), "testid", DeviceType.Android);
    }

    /// <summary>
    /// Story: A user chose to keep trust in one of their current trusted devices, but not in another one of their
    /// devices. We will rotate the trust of the currently signed in device as well as the device they chose but will
    /// remove the trust of the device they didn't give new keys for.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateDevicesTrustAsync_Works(
        SutProvider<DeviceService> sutProvider,
        Guid currentUserId,
        Device deviceOne,
        Device deviceTwo,
        Device deviceThree)
    {
        SetupOldTrust(deviceOne);
        SetupOldTrust(deviceTwo);
        SetupOldTrust(deviceThree);

        deviceOne.Identifier = "current_device";

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(currentUserId)
            .Returns(new List<Device>
            {
                deviceOne,
                deviceTwo,
                deviceThree,
            });

        var currentDeviceModel = new DeviceKeysUpdateRequestModel
        {
            EncryptedPublicKey = "current_encrypted_public_key",
            EncryptedUserKey = "current_encrypted_user_key",
        };

        var alteredDeviceModels = new List<OtherDeviceKeysUpdateRequestModel>
        {
            new OtherDeviceKeysUpdateRequestModel
            {
                DeviceId = deviceTwo.Id,
                EncryptedPublicKey = "encrypted_public_key_two",
                EncryptedUserKey = "encrypted_user_key_two",
            },
        };

        await sutProvider.Sut.UpdateDevicesTrustAsync("current_device", currentUserId, currentDeviceModel, alteredDeviceModels);

        // Updating trust, "current" or "other" only needs to change the EncryptedPublicKey & EncryptedUserKey
        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d =>
                d.Id == deviceOne.Id &&
                d.EncryptedPublicKey == "current_encrypted_public_key" &&
                d.EncryptedUserKey == "current_encrypted_user_key" &&
                d.EncryptedPrivateKey == "old_private_deviceOne"));

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d =>
                d.Id == deviceTwo.Id &&
                d.EncryptedPublicKey == "encrypted_public_key_two" &&
                d.EncryptedUserKey == "encrypted_user_key_two" &&
                d.EncryptedPrivateKey == "old_private_deviceTwo"));

        // Clearing trust should remove all key values
        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d =>
                d.Id == deviceThree.Id &&
                d.EncryptedPublicKey == null &&
                d.EncryptedUserKey == null &&
                d.EncryptedPrivateKey == null));

        // Should have recieved a total of 3 calls, the ones asserted above
        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(3)
            .UpsertAsync(Arg.Any<Device>());

        static void SetupOldTrust(Device device, [CallerArgumentExpression(nameof(device))] string expression = null)
        {
            device.EncryptedPublicKey = $"old_public_{expression}";
            device.EncryptedPrivateKey = $"old_private_{expression}";
            device.EncryptedUserKey = $"old_user_{expression}";
        }
    }

    /// <summary>
    /// Story: This could result from a poor implementation of this method, if they attempt add trust to a device
    /// that doesn't already have trust. They would have to create brand new values and for that values to be accurate
    /// they would technically have all the values needed to trust a device, that is why we don't consider this bad
    /// enough to throw but do skip it because we'd rather keep number of ways for trust to be added to the endpoint we
    /// already have.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateDevicesTrustAsync_DoesNotUpdateUntrustedDevices(
        SutProvider<DeviceService> sutProvider,
        Guid currentUserId,
        Device deviceOne,
        Device deviceTwo)
    {
        deviceOne.Identifier = "current_device";

        // Make deviceTwo untrusted
        deviceTwo.EncryptedUserKey = string.Empty;
        deviceTwo.EncryptedPublicKey = string.Empty;
        deviceTwo.EncryptedPrivateKey = string.Empty;

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(currentUserId)
            .Returns(new List<Device>
            {
                deviceOne,
                deviceTwo,
            });

        var currentDeviceModel = new DeviceKeysUpdateRequestModel
        {
            EncryptedPublicKey = "current_encrypted_public_key",
            EncryptedUserKey = "current_encrypted_user_key",
        };

        var alteredDeviceModels = new List<OtherDeviceKeysUpdateRequestModel>
        {
            new OtherDeviceKeysUpdateRequestModel
            {
                DeviceId = deviceTwo.Id,
                EncryptedPublicKey = "encrypted_public_key_two",
                EncryptedUserKey = "encrypted_user_key_two",
            },
        };

        await sutProvider.Sut.UpdateDevicesTrustAsync("current_device", currentUserId, currentDeviceModel, alteredDeviceModels);

        // Check that UpsertAsync was called for the trusted device
        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d =>
                d.Id == deviceOne.Id &&
                d.EncryptedPublicKey == "current_encrypted_public_key" &&
                d.EncryptedUserKey == "current_encrypted_user_key"));

        // Check that UpsertAsync was not called for the untrusted device
        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .UpsertAsync(Arg.Is<Device>(d => d.Id == deviceTwo.Id));
    }

    /// <summary>
    /// Story: This should only happen if someone were to take the access token from a different device and try to rotate
    /// a device that they don't actually have.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateDevicesTrustAsync_ThrowsNotFoundException_WhenCurrentDeviceIdentifierDoesNotExist(
        SutProvider<DeviceService> sutProvider,
        Guid currentUserId,
        Device deviceOne,
        Device deviceTwo)
    {
        deviceOne.Identifier = "some_other_device";
        deviceTwo.Identifier = "another_device";

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(currentUserId)
            .Returns(new List<Device>
            {
                deviceOne,
                deviceTwo,
            });

        var currentDeviceModel = new DeviceKeysUpdateRequestModel
        {
            EncryptedPublicKey = "current_encrypted_public_key",
            EncryptedUserKey = "current_encrypted_user_key",
        };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateDevicesTrustAsync("current_device", currentUserId, currentDeviceModel,
                Enumerable.Empty<OtherDeviceKeysUpdateRequestModel>()));
    }

    /// <summary>
    /// Story: This should only happen from a poorly implemented user of this method but important to enforce someone
    /// using the method correctly, a device should only be rotated intentionally and including it as both the current
    /// device and one of the users other device would mean they could rotate it twice and we aren't sure
    /// which one they would want to win out.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateDevicesTrustAsync_ThrowsBadRequestException_WhenCurrentDeviceIsIncludedInAlteredDevices(
        SutProvider<DeviceService> sutProvider,
        Guid currentUserId,
        Device deviceOne,
        Device deviceTwo)
    {
        deviceOne.Identifier = "current_device";

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(currentUserId)
            .Returns(new List<Device>
            {
                deviceOne,
                deviceTwo,
            });

        var currentDeviceModel = new DeviceKeysUpdateRequestModel
        {
            EncryptedPublicKey = "current_encrypted_public_key",
            EncryptedUserKey = "current_encrypted_user_key",
        };

        var alteredDeviceModels = new List<OtherDeviceKeysUpdateRequestModel>
        {
            new OtherDeviceKeysUpdateRequestModel
            {
                DeviceId = deviceOne.Id, // current device is included in alteredDevices
                EncryptedPublicKey = "encrypted_public_key_one",
                EncryptedUserKey = "encrypted_user_key_one",
            },
        };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.UpdateDevicesTrustAsync("current_device", currentUserId, currentDeviceModel, alteredDeviceModels));
    }
}

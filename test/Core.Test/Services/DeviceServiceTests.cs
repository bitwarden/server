using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    /// 
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateDevicesTrustAsync_Works(
        SutProvider<DeviceService> sutProvider,
        Guid currentUserId,
        Device deviceOne,
        Device deviceTwo,
        Device deviceThree)
    {
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

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d => d.Id == deviceOne.Id && d.EncryptedPublicKey == "current_encrypted_public_key"));

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d => d.Id == deviceTwo.Id && d.EncryptedPublicKey == "encrypted_public_key_two"));

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Device>(d => d.Id == deviceThree.Id && d.EncryptedPublicKey == null));

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(3)
            .UpsertAsync(Arg.Any<Device>());
    }
}

using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class DeviceRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(IDeviceRepository deviceRepository,
        IServiceProvider services)
    {
        var user = await services.CreateUserAsync();

        var identifier = Guid.NewGuid().ToString();

        var createdDevice = await deviceRepository.CreateAsync(new Device
        {
            UserId = user.Id,
            Identifier = identifier,
            Name = "Android",
            Type = DeviceType.Android,
            PushToken = "my_push_token",
            EncryptedPrivateKey = "encrypted_private_key",
            EncryptedPublicKey = "encrypted_public_key",
            EncryptedUserKey = "encrypted_user_key",
        });

        // Assert that we get the Id back right away so that we know we set the Id client side
        Assert.NotEqual(createdDevice.Id, Guid.Empty);

        // Assert that we can find one from the database
        var device = await deviceRepository.GetByIdAsync(createdDevice.Id);
        Assert.NotNull(device);

        // Assert the found item has all the data we expect
        Assert.Equal(user.Id, device.UserId);
        Assert.Equal(identifier, device.Identifier);
        Assert.Equal("Android", device.Name);
        Assert.Equal(DeviceType.Android, device.Type);
        Assert.Equal("my_push_token", device.PushToken);
        Assert.Equal("encrypted_private_key", device.EncryptedPrivateKey);
        Assert.Equal("encrypted_public_key", device.EncryptedPublicKey);
        Assert.Equal("encrypted_user_key", device.EncryptedUserKey);

        // Assert the items returned from CreateAsync match what is found by id
        Assert.Equal(createdDevice.Id, device.Id);
        Assert.Equal(createdDevice.Identifier, device.Identifier);
        Assert.Equal(createdDevice.Name, device.Name);
        Assert.Equal(createdDevice.Type, device.Type);
        Assert.Equal(createdDevice.PushToken, device.PushToken);
        Assert.Equal(createdDevice.EncryptedPrivateKey, device.EncryptedPrivateKey);
        Assert.Equal(createdDevice.EncryptedPublicKey, device.EncryptedPublicKey);
        Assert.Equal(createdDevice.EncryptedUserKey, device.EncryptedUserKey);
    }
}

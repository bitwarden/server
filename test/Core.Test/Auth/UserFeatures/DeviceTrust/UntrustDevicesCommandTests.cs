using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class UntrustDevicesCommandTests
{
    [Theory, BitAutoData]
    public async Task SetsKeysToNull(SutProvider<UntrustDevicesCommand> sutProvider, User user)
    {
        var deviceId = Guid.NewGuid();
        // Arrange
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([new Device
            {
                Id = deviceId,
                EncryptedPrivateKey = "encryptedPrivateKey",
                EncryptedPublicKey = "encryptedPublicKey",
                EncryptedUserKey = "encryptedUserKey"
            }]);

        // Act
        await sutProvider.Sut.UntrustDevices(user, new List<Guid> { deviceId });

        // Assert
        await sutProvider.GetDependency<IDeviceRepository>()
            .Received()
            .UpsertAsync(Arg.Is<Device>(d =>
                d.Id == deviceId &&
                d.EncryptedPrivateKey == null &&
                d.EncryptedPublicKey == null &&
                d.EncryptedUserKey == null));
    }

    [Theory, BitAutoData]
    public async Task RejectsWrongUser(SutProvider<UntrustDevicesCommand> sutProvider, User user)
    {
        var deviceId = Guid.NewGuid();
        // Arrange
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([]);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await sutProvider.Sut.UntrustDevices(user, new List<Guid> { deviceId }));
    }
}

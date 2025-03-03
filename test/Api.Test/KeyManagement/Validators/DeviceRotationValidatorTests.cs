using Bit.Api.KeyManagement.Validators;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Validators;

[SutProviderCustomize]
public class DeviceRotationValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_SentDevicesAreEmptyButDatabaseDevicesAreNot_Throws(
        SutProvider<DeviceRotationValidator> sutProvider, User user, IEnumerable<OtherDeviceKeysUpdateRequestModel> devices)
    {
        var userCiphers = devices.Select(c => new Device { Id = c.DeviceId, EncryptedPrivateKey = "EncryptedPrivateKey", EncryptedPublicKey = "EncryptedPublicKey", EncryptedUserKey = "EncryptedUserKey" }).ToList();
        sutProvider.GetDependency<IDeviceRepository>().GetManyByUserIdAsync(user.Id)
            .Returns(userCiphers);
        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.ValidateAsync(user, Enumerable.Empty<OtherDeviceKeysUpdateRequestModel>()));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_SentDevicesTrustedButDatabaseUntrusted_Throws(
        SutProvider<DeviceRotationValidator> sutProvider, User user, IEnumerable<OtherDeviceKeysUpdateRequestModel> devices)
    {
        var userCiphers = devices.Select(c => new Device { Id = c.DeviceId, EncryptedPrivateKey = "Key", EncryptedPublicKey = "Key", EncryptedUserKey = "Key" }).ToList();
        sutProvider.GetDependency<IDeviceRepository>().GetManyByUserIdAsync(user.Id)
            .Returns(userCiphers);
        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.ValidateAsync(user, [
            new OtherDeviceKeysUpdateRequestModel { DeviceId = userCiphers.First().Id, EncryptedPublicKey = null, EncryptedUserKey = null }
        ]));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_Validates(
        SutProvider<DeviceRotationValidator> sutProvider, User user, IEnumerable<OtherDeviceKeysUpdateRequestModel> devices)
    {
        var userCiphers = devices.Select(c => new Device { Id = c.DeviceId, EncryptedPrivateKey = "Key", EncryptedPublicKey = "Key", EncryptedUserKey = "Key" }).ToList().Slice(0, 1);
        sutProvider.GetDependency<IDeviceRepository>().GetManyByUserIdAsync(user.Id)
            .Returns(userCiphers);
        Assert.NotEmpty(await sutProvider.Sut.ValidateAsync(user, [
            new OtherDeviceKeysUpdateRequestModel { DeviceId = userCiphers.First().Id, EncryptedPublicKey = "Key", EncryptedUserKey = "Key" }
        ]));
    }
}

using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Identity.IdentityServer;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class UserDecryptionOptionsBuilderTests
{
    private readonly UserDecryptionOptionsBuilder _builder;

    public UserDecryptionOptionsBuilderTests()
    {
        _builder = new UserDecryptionOptionsBuilder();
    }

    [Theory, BitAutoData]
    public void ForUser_WhenUserHasMasterPassword_ShouldReturnMasterPasswordOption(User user)
    {
        user.MasterPassword = "password";

        var result = _builder.ForUser(user).Build();

        Assert.True(result.HasMasterPassword);
    }

    [Theory, BitAutoData]
    public void ForUser_WhenUserDoesNotHaveMasterPassword_ShouldNotReturnMasterPasswordOption(User user)
    {
        user.MasterPassword = null;

        var result = _builder.ForUser(user).Build();

        Assert.False(result.HasMasterPassword);
    }

    [Theory, BitAutoData]
    public void Build_WhenKeyConnectorIsEnabled_ShouldReturnKeyConnectorOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.KeyConnector;
        ssoConfig.Data = configurationData.Serialize();

        var result = _builder.WithSso(ssoConfig).Build();

        Assert.NotNull(result.KeyConnectorOption);
        Assert.Equal(configurationData.KeyConnectorUrl, result.KeyConnectorOption!.KeyConnectorUrl);
    }

    [Theory, BitAutoData]
    public void Build_WhenTrustedDeviceIsEnabledAndDeviceIsTrusted_ShouldReturnTrustedDeviceOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        device.EncryptedPrivateKey = "encryptedPrivateKey";
        device.EncryptedPublicKey = "encryptedPublicKey";
        device.EncryptedUserKey = "encryptedUserKey";

        var result = _builder.WithSso(ssoConfig).WithDevice(device).Build();

        Assert.NotNull(result.TrustedDeviceOption);
        Assert.Equal(device.EncryptedPrivateKey, result.TrustedDeviceOption!.EncryptedPrivateKey);
        Assert.Equal(device.EncryptedUserKey, result.TrustedDeviceOption!.EncryptedUserKey);
    }
}

using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Identity.IdentityServer;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class UserDecryptionOptionsBuilderTests
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly UserDecryptionOptionsBuilder _builder;

    public UserDecryptionOptionsBuilderTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _featureService = Substitute.For<IFeatureService>();
        _builder = new UserDecryptionOptionsBuilder(_currentContext, _featureService);
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
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
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

    // TODO: Remove when FeatureFlagKeys.TrustedDeviceEncryption is removed
    [Theory, BitAutoData]
    public void Build_WhenTrustedDeviceIsEnabledButFeatureFlagIsDisabled_ShouldNotReturnTrustedDeviceOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(false);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        device.EncryptedPrivateKey = "encryptedPrivateKey";
        device.EncryptedPublicKey = "encryptedPublicKey";
        device.EncryptedUserKey = "encryptedUserKey";

        var result = _builder.WithSso(ssoConfig).WithDevice(device).Build();

        Assert.Null(result.TrustedDeviceOption);
    }
}

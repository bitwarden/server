using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class PolicyValidatorHelpersTests
{
    [Fact]
    public void ValidateDecryptionOptionsNotEnabled_RequiredByKeyConnector_ValidationError()
    {
        var ssoConfig = new SsoConfig();
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });

        var result = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.KeyConnector]);

        Assert.Contains("Key Connector is enabled", result);
    }

    [Fact]
    public void ValidateDecryptionOptionsNotEnabled_RequiredByTDE_ValidationError()
    {
        var ssoConfig = new SsoConfig();
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption });

        var result = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.TrustedDeviceEncryption]);

        Assert.Contains("Trusted device encryption is on", result);
    }

    [Fact]
    public void ValidateDecryptionOptionsNotEnabled_NullSsoConfig_NoValidationError()
    {
        var ssoConfig = new SsoConfig();
        var result = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.KeyConnector]);

        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void ValidateDecryptionOptionsNotEnabled_RequiredOptionNotEnabled_NoValidationError()
    {
        var ssoConfig = new SsoConfig();
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });

        var result = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.TrustedDeviceEncryption]);

        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void ValidateDecryptionOptionsNotEnabled_SsoConfigDisabled_NoValidationError()
    {
        var ssoConfig = new SsoConfig();
        ssoConfig.Enabled = false;
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });

        var result = ssoConfig.ValidateDecryptionOptionsNotEnabled([MemberDecryptionType.KeyConnector]);

        Assert.True(string.IsNullOrEmpty(result));
    }
}

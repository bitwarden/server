using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.IdentityServer;
using Bit.Identity.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class UserDecryptionOptionsBuilderTests
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly UserDecryptionOptionsBuilder _builder;

    public UserDecryptionOptionsBuilderTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _featureService = Substitute.For<IFeatureService>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _builder = new UserDecryptionOptionsBuilder(_currentContext, _featureService, _deviceRepository, _organizationUserRepository);
    }

    [Theory, BitAutoData]
    public async Task ForUser_WhenUserHasMasterPassword_ShouldReturnMasterPasswordOption(User user)
    {
        user.MasterPassword = "password";

        var result = await _builder.ForUser(user).BuildAsync();

        Assert.True(result.HasMasterPassword);
    }

    [Theory, BitAutoData]
    public async Task ForUser_WhenUserDoesNotHaveMasterPassword_ShouldNotReturnMasterPasswordOption(User user)
    {
        user.MasterPassword = null;

        var result = await _builder.ForUser(user).BuildAsync();

        Assert.False(result.HasMasterPassword);
    }

    [Theory, BitAutoData]
    public async Task WithWebAuthnLoginCredential_WhenCredentialDoesNotContainPrfKeys_ShouldNotReturnPrfOption(WebAuthnCredential credential)
    {
        credential.EncryptedPrivateKey = null;
        credential.EncryptedPublicKey = null;
        credential.EncryptedUserKey = null;

        var result = await _builder.WithWebAuthnLoginCredential(credential).BuildAsync();

        Assert.Null(result.WebAuthnPrfOption);
    }

    [Theory, BitAutoData]
    public async Task WithWebAuthnLoginCredential_WhenCredentialContainsPrfKeys_ShouldReturnPrfOption(WebAuthnCredential credential)
    {
        credential.EncryptedPrivateKey = "encryptedPrivateKey";
        credential.EncryptedPublicKey = "encryptedPublicKey";
        credential.EncryptedUserKey = "encryptedUserKey";

        var result = await _builder.WithWebAuthnLoginCredential(credential).BuildAsync();

        Assert.NotNull(result.WebAuthnPrfOption);
        Assert.Equal(credential.EncryptedPrivateKey, result.WebAuthnPrfOption!.EncryptedPrivateKey);
        Assert.Equal(credential.EncryptedUserKey, result.WebAuthnPrfOption!.EncryptedUserKey);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenKeyConnectorIsEnabled_ShouldReturnKeyConnectorOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.KeyConnector;
        ssoConfig.Data = configurationData.Serialize();

        var result = await _builder.WithSso(ssoConfig).BuildAsync();

        Assert.NotNull(result.KeyConnectorOption);
        Assert.Equal(configurationData.KeyConnectorUrl, result.KeyConnectorOption!.KeyConnectorUrl);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenTrustedDeviceIsEnabled_ShouldReturnTrustedDeviceOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();

        var result = await _builder.WithSso(ssoConfig).WithDevice(device).BuildAsync();

        Assert.NotNull(result.TrustedDeviceOption);
        Assert.False(result.TrustedDeviceOption!.HasAdminApproval);
        Assert.False(result.TrustedDeviceOption!.HasLoginApprovingDevice);
        Assert.False(result.TrustedDeviceOption!.HasManageResetPasswordPermission);
    }

    // TODO: Remove when FeatureFlagKeys.TrustedDeviceEncryption is removed
    [Theory, BitAutoData]
    public async Task Build_WhenTrustedDeviceIsEnabledButFeatureFlagIsDisabled_ShouldNotReturnTrustedDeviceOptions(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(false);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();

        var result = await _builder.WithSso(ssoConfig).WithDevice(device).BuildAsync();

        Assert.Null(result.TrustedDeviceOption);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenDeviceIsTrusted_ShouldReturnKeys(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        device.EncryptedPrivateKey = "encryptedPrivateKey";
        device.EncryptedPublicKey = "encryptedPublicKey";
        device.EncryptedUserKey = "encryptedUserKey";

        var result = await _builder.WithSso(ssoConfig).WithDevice(device).BuildAsync();

        Assert.Equal(device.EncryptedPrivateKey, result.TrustedDeviceOption?.EncryptedPrivateKey);
        Assert.Equal(device.EncryptedUserKey, result.TrustedDeviceOption?.EncryptedUserKey);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenHasLoginApprovingDevice_ShouldApprovingDeviceTrue(SsoConfig ssoConfig, SsoConfigurationData configurationData, User user, Device device, Device approvingDevice)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        approvingDevice.Type = LoginApprovingDeviceTypes.Types.First();
        _deviceRepository.GetManyByUserIdAsync(user.Id).Returns(new Device[] { approvingDevice });

        var result = await _builder.ForUser(user).WithSso(ssoConfig).WithDevice(device).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasLoginApprovingDevice);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenManageResetPasswordPermissions_ShouldReturnHasManageResetPasswordPermissionTrue(
        SsoConfig ssoConfig,
        SsoConfigurationData configurationData,
        CurrentContextOrganization organization)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        ssoConfig.OrganizationId = organization.Id;
        _currentContext.Organizations.Returns(new List<CurrentContextOrganization>(new CurrentContextOrganization[] { organization }));
        _currentContext.ManageResetPassword(organization.Id).Returns(true);

        var result = await _builder.WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasManageResetPasswordPermission);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenUserHasEnrolledIntoPasswordReset_ShouldReturnHasAdminApprovalTrue(
        SsoConfig ssoConfig,
        SsoConfigurationData configurationData,
        OrganizationUser organizationUser,
        User user)
    {
        _featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext).Returns(true);
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        organizationUser.ResetPasswordKey = "resetPasswordKey";
        _organizationUserRepository.GetByOrganizationAsync(ssoConfig.OrganizationId, user.Id).Returns(organizationUser);

        var result = await _builder.ForUser(user).WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasAdminApproval);
    }
}

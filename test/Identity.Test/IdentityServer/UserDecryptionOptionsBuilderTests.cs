using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Identity.IdentityServer;
using Bit.Identity.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class UserDecryptionOptionsBuilderTests
{
    private readonly ICurrentContext _currentContext;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly UserDecryptionOptionsBuilder _builder;

    public UserDecryptionOptionsBuilderTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _builder = new UserDecryptionOptionsBuilder(_currentContext, _deviceRepository, _organizationUserRepository);
    }

    [Theory]
    [BitAutoData(true, true, true)]    // All keys are non-null
    [BitAutoData(false, false, false)] // All keys are null
    [BitAutoData(false, false, true)]  // EncryptedUserKey is non-null, others are null
    [BitAutoData(false, true, false)]  // EncryptedPublicKey is non-null, others are null
    [BitAutoData(true, false, false)]  // EncryptedPrivateKey is non-null, others are null
    [BitAutoData(true, false, true)]   // EncryptedPrivateKey and EncryptedUserKey are non-null, EncryptedPublicKey is null
    [BitAutoData(true, true, false)]   // EncryptedPrivateKey and EncryptedPublicKey are non-null, EncryptedUserKey is null
    [BitAutoData(false, true, true)]   // EncryptedPublicKey and EncryptedUserKey are non-null, EncryptedPrivateKey is null
    public async Task WithWebAuthnLoginCredential_VariousKeyCombinations_ShouldReturnCorrectPrfOption(
        bool hasEncryptedPrivateKey,
        bool hasEncryptedPublicKey,
        bool hasEncryptedUserKey,
        WebAuthnCredential credential)
    {
        credential.EncryptedPrivateKey = hasEncryptedPrivateKey ? "encryptedPrivateKey" : null;
        credential.EncryptedPublicKey = hasEncryptedPublicKey ? "encryptedPublicKey" : null;
        credential.EncryptedUserKey = hasEncryptedUserKey ? "encryptedUserKey" : null;

        var result = await _builder.WithWebAuthnLoginCredential(credential).BuildAsync();

        if (credential.GetPrfStatus() == WebAuthnPrfStatus.Enabled)
        {
            Assert.NotNull(result.WebAuthnPrfOption);
            Assert.Equal(credential.EncryptedPrivateKey, result.WebAuthnPrfOption!.EncryptedPrivateKey);
            Assert.Equal(credential.EncryptedUserKey, result.WebAuthnPrfOption!.EncryptedUserKey);
        }
        else
        {
            Assert.Null(result.WebAuthnPrfOption);
        }
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
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();

        var result = await _builder.WithSso(ssoConfig).WithDevice(device).BuildAsync();

        Assert.NotNull(result.TrustedDeviceOption);
        Assert.False(result.TrustedDeviceOption!.HasAdminApproval);
        Assert.False(result.TrustedDeviceOption!.HasLoginApprovingDevice);
        Assert.False(result.TrustedDeviceOption!.HasManageResetPasswordPermission);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenDeviceIsTrusted_ShouldReturnKeys(SsoConfig ssoConfig, SsoConfigurationData configurationData, Device device)
    {
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
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        ssoConfig.OrganizationId = organization.Id;
        _currentContext.Organizations.Returns(new List<CurrentContextOrganization>(new CurrentContextOrganization[] { organization }));
        _currentContext.ManageResetPassword(organization.Id).Returns(true);

        var result = await _builder.WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasManageResetPasswordPermission);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenIsOwnerInvite_ShouldReturnHasManageResetPasswordPermissionTrue(
        SsoConfig ssoConfig,
        SsoConfigurationData configurationData,
        OrganizationUser organizationUser,
        User user)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        organizationUser.Type = OrganizationUserType.Owner;
        _organizationUserRepository.GetByOrganizationAsync(ssoConfig.OrganizationId, user.Id).Returns(organizationUser);

        var result = await _builder.ForUser(user).WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasManageResetPasswordPermission);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenIsAdminInvite_ShouldReturnHasManageResetPasswordPermissionTrue(
        SsoConfig ssoConfig,
        SsoConfigurationData configurationData,
        OrganizationUser organizationUser,
        User user)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        organizationUser.Type = OrganizationUserType.Admin;
        _organizationUserRepository.GetByOrganizationAsync(ssoConfig.OrganizationId, user.Id).Returns(organizationUser);

        var result = await _builder.ForUser(user).WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasManageResetPasswordPermission);
    }

    [Theory, BitAutoData]
    public async Task Build_WhenUserHasEnrolledIntoPasswordReset_ShouldReturnHasAdminApprovalTrue(
        SsoConfig ssoConfig,
        SsoConfigurationData configurationData,
        OrganizationUser organizationUser,
        User user)
    {
        configurationData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.Data = configurationData.Serialize();
        organizationUser.ResetPasswordKey = "resetPasswordKey";
        _organizationUserRepository.GetByOrganizationAsync(ssoConfig.OrganizationId, user.Id).Returns(organizationUser);

        var result = await _builder.ForUser(user).WithSso(ssoConfig).BuildAsync();

        Assert.True(result.TrustedDeviceOption?.HasAdminApproval);
    }
}

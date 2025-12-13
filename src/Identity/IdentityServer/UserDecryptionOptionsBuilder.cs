using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Response;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Identity.Utilities;

namespace Bit.Identity.IdentityServer;

#nullable enable
/// <summary>
/// Used to create a list of all possible ways the newly authenticated user can decrypt their vault contents
///
/// Note: Do not use this as an injected service if you intend to build multiple independent UserDecryptionOptions
/// </summary>
public class UserDecryptionOptionsBuilder : IUserDecryptionOptionsBuilder
{
    private readonly ICurrentContext _currentContext;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ILoginApprovingClientTypes _loginApprovingClientTypes;
    private UserDecryptionOptions _options = new UserDecryptionOptions();
    private User _user = null!;
    private SsoConfig? _ssoConfig;
    private Device? _device;

    public UserDecryptionOptionsBuilder(
        ICurrentContext currentContext,
        IDeviceRepository deviceRepository,
        IOrganizationUserRepository organizationUserRepository,
        ILoginApprovingClientTypes loginApprovingClientTypes
    )
    {
        _currentContext = currentContext;
        _deviceRepository = deviceRepository;
        _organizationUserRepository = organizationUserRepository;
        _loginApprovingClientTypes = loginApprovingClientTypes;
    }

    public IUserDecryptionOptionsBuilder ForUser(User user)
    {
        _user = user;
        return this;
    }

    public IUserDecryptionOptionsBuilder WithSso(SsoConfig ssoConfig)
    {
        _ssoConfig = ssoConfig;
        return this;
    }

    public IUserDecryptionOptionsBuilder WithDevice(Device device)
    {
        _device = device;
        return this;
    }

    public IUserDecryptionOptionsBuilder WithWebAuthnLoginCredential(WebAuthnCredential credential)
    {
        if (credential.GetPrfStatus() == WebAuthnPrfStatus.Enabled)
        {
            _options.WebAuthnPrfOption =
                new WebAuthnPrfDecryptionOption(credential.EncryptedPrivateKey, credential.EncryptedUserKey);
        }

        return this;
    }

    public async Task<UserDecryptionOptions> BuildAsync()
    {
        BuildMasterPasswordUnlock();
        BuildKeyConnectorOptions();
        await BuildTrustedDeviceOptionsAsync();

        return _options;
    }

    private void BuildKeyConnectorOptions()
    {
        if (_ssoConfig == null)
        {
            return;
        }

        var ssoConfigurationData = _ssoConfig.GetData();
        if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } &&
            !string.IsNullOrEmpty(ssoConfigurationData.KeyConnectorUrl))
        {
            _options.KeyConnectorOption = new KeyConnectorUserDecryptionOption(ssoConfigurationData.KeyConnectorUrl);
        }
    }

    private async Task BuildTrustedDeviceOptionsAsync()
    {
        // TrustedDeviceEncryption only exists for SSO, if that changes then these guards should change
        if (_ssoConfig == null)
        {
            return;
        }

        var isTdeActive = _ssoConfig.GetData() is
        { MemberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption };
        var isTdeOffboarding = !_user.HasMasterPassword() && _device != null && _device.IsTrusted() && !isTdeActive;
        if (!isTdeActive && !isTdeOffboarding)
        {
            return;
        }

        string? encryptedPrivateKey = null;
        string? encryptedUserKey = null;
        if (_device != null && _device.IsTrusted())
        {
            encryptedPrivateKey = _device.EncryptedPrivateKey;
            encryptedUserKey = _device.EncryptedUserKey;
        }

        var hasLoginApprovingDevice = false;
        if (_device != null)
        {
            var allDevices = await _deviceRepository.GetManyByUserIdAsync(_user.Id);
            // Checks if the current user has any devices that are capable of approving login with device requests
            // except for their current device.
            hasLoginApprovingDevice = allDevices.Any(d =>
                d.Identifier != _device.Identifier &&
                _loginApprovingClientTypes.TypesThatCanApprove.Contains(DeviceTypes.ToClientType(d.Type)));
        }

        // Just-in-time-provisioned users, which can include users invited to a TDE organization with SSO and granted
        // the Admin/Owner role or Custom user role with ManageResetPassword permission, will not have claims available
        // in context to reflect this permission if granted as part of an invite for the current organization.
        // Therefore, as written today, CurrentContext will not surface those permissions for those users.
        // In order to make this check accurate at first login for all applicable cases, we have to go back to the
        // database record.
        // In the TDE flow, the users will have been JIT-provisioned at SSO callback time, and the relationship between
        // user and organization user will have been codified.
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(_ssoConfig.OrganizationId, _user.Id);
        var hasManageResetPasswordPermission = await EvaluateHasManageResetPasswordPermission();

        // They are only able to be approved by an admin if they have enrolled is reset password
        var hasAdminApproval = organizationUser != null && !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);

        _options.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
            hasAdminApproval,
            hasLoginApprovingDevice,
            hasManageResetPasswordPermission,
            isTdeOffboarding,
            encryptedPrivateKey,
            encryptedUserKey);
        return;

        /// Determine if the user has manage reset password permission,  
        /// as post-SSO logic requires it for forcing users with this permission to set a password.
        async Task<bool> EvaluateHasManageResetPasswordPermission()
        {
            if (organizationUser == null)
            {
                return false;
            }

            var organizationUserHasResetPasswordPermission =
                // The repository will pull users in all statuses, so we also need to ensure that revoked-status users do not have
                // permissions sent down.
                organizationUser.Status is OrganizationUserStatusType.Invited or OrganizationUserStatusType.Accepted or
                    OrganizationUserStatusType.Confirmed &&
                // Admins and owners get ManageResetPassword functionally "for free" through their role.
                (organizationUser.Type is OrganizationUserType.Admin or OrganizationUserType.Owner ||
                 // Custom users can have the ManagePasswordReset permission assigned directly.
                 organizationUser.GetPermissions() is { ManageResetPassword: true });

            return organizationUserHasResetPasswordPermission ||
                   // A provider user for the given organization gets ManageResetPassword through that relationship.
                   await _currentContext.ProviderUserForOrgAsync(_ssoConfig.OrganizationId);
        }
    }

    private void BuildMasterPasswordUnlock()
    {
        if (_user.HasMasterPassword())
        {
            _options.HasMasterPassword = true;
            _options.MasterPasswordUnlock = new MasterPasswordUnlockResponseModel
            {
                Kdf = new MasterPasswordUnlockKdfResponseModel
                {
                    KdfType = _user.Kdf,
                    Iterations = _user.KdfIterations,
                    Memory = _user.KdfMemory,
                    Parallelism = _user.KdfParallelism
                },
                MasterKeyEncryptedUserKey = _user.Key!,
                Salt = _user.Email.ToLowerInvariant()
            };
        }
        else
        {
            _options.HasMasterPassword = false;
        }
    }
}

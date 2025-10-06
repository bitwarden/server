using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Response;
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
            _options.WebAuthnPrfOption = new WebAuthnPrfDecryptionOption(
                credential.EncryptedPrivateKey,
                credential.EncryptedUserKey,
                credential.CredentialId,
                [] // Stored credentials currently lack Transports, just send an empty array for now
            );
        }
        return this;
    }

    public async Task<UserDecryptionOptions> BuildAsync()
    {
        BuildMasterPasswordUnlock();
        BuildKeyConnectorOptions();
        await BuildTrustedDeviceOptions();

        return _options;
    }

    private void BuildKeyConnectorOptions()
    {
        if (_ssoConfig == null)
        {
            return;
        }

        var ssoConfigurationData = _ssoConfig.GetData();
        if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } && !string.IsNullOrEmpty(ssoConfigurationData.KeyConnectorUrl))
        {
            _options.KeyConnectorOption = new KeyConnectorUserDecryptionOption(ssoConfigurationData.KeyConnectorUrl);
        }
    }

    private async Task BuildTrustedDeviceOptions()
    {
        // TrustedDeviceEncryption only exists for SSO, if that changes then these guards should change
        if (_ssoConfig == null)
        {
            return;
        }

        var isTdeActive = _ssoConfig.GetData() is { MemberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption };
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
            // Checks if the current user has any devices that are capable of approving login with device requests except for
            // their current device.
            // NOTE: this doesn't check for if the users have configured the devices to be capable of approving requests as that is a client side setting.
            hasLoginApprovingDevice = allDevices.Any(d => d.Identifier != _device.Identifier && _loginApprovingClientTypes.TypesThatCanApprove.Contains(DeviceTypes.ToClientType(d.Type)));
        }

        // Determine if user has manage reset password permission as post sso logic requires it for forcing users with this permission to set a MP
        var hasManageResetPasswordPermission = false;
        // when a user is being created via JIT provisioning, they will not have any orgs so we can't assume we will have orgs here
        if (_currentContext.Organizations != null && _currentContext.Organizations.Any(o => o.Id == _ssoConfig.OrganizationId))
        {
            // TDE requires single org so grabbing first org & id is fine.
            hasManageResetPasswordPermission = await _currentContext.ManageResetPassword(_ssoConfig!.OrganizationId);
        }

        // If sso configuration data is not null then I know for sure that ssoConfiguration isn't null
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(_ssoConfig.OrganizationId, _user.Id);

        hasManageResetPasswordPermission |= organizationUser != null && (organizationUser.Type == OrganizationUserType.Owner || organizationUser.Type == OrganizationUserType.Admin);
        // They are only able to be approved by an admin if they have enrolled is reset password
        var hasAdminApproval = organizationUser != null && !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);

        _options.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
            hasAdminApproval,
            hasLoginApprovingDevice,
            hasManageResetPasswordPermission,
            isTdeOffboarding,
            encryptedPrivateKey,
            encryptedUserKey);
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

using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Utilities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
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

    private UserDecryptionOptions _options = new UserDecryptionOptions();
    private User? _user;
    private Core.Auth.Entities.SsoConfig? _ssoConfig;
    private Device? _device;

    public UserDecryptionOptionsBuilder(
        ICurrentContext currentContext,
        IDeviceRepository deviceRepository,
        IOrganizationUserRepository organizationUserRepository
    )
    {
        _currentContext = currentContext;
        _deviceRepository = deviceRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    public IUserDecryptionOptionsBuilder ForUser(User user)
    {
        _options.HasMasterPassword = user.HasMasterPassword();
        _user = user;
        return this;
    }

    public IUserDecryptionOptionsBuilder WithSso(Core.Auth.Entities.SsoConfig ssoConfig)
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
            _options.WebAuthnPrfOption = new WebAuthnPrfDecryptionOption(credential.EncryptedPrivateKey, credential.EncryptedUserKey);
        }
        return this;
    }

    public async Task<UserDecryptionOptions> BuildAsync()
    {
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
        var isTdeOffboarding = _user != null && !_user.HasMasterPassword() && _device != null && _device.IsTrusted() && !isTdeActive;
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
        if (_device != null && _user != null)
        {
            var allDevices = await _deviceRepository.GetManyByUserIdAsync(_user.Id);
            // Checks if the current user has any devices that are capable of approving login with device requests except for
            // their current device.
            // NOTE: this doesn't check for if the users have configured the devices to be capable of approving requests as that is a client side setting.
            hasLoginApprovingDevice = allDevices
                .Where(d => d.Identifier != _device.Identifier && LoginApprovingDeviceTypes.Types.Contains(d.Type))
                .Any();
        }

        // Determine if user has manage reset password permission as post sso logic requires it for forcing users with this permission to set a MP
        var hasManageResetPasswordPermission = false;
        // when a user is being created via JIT provisioning, they will not have any orgs so we can't assume we will have orgs here
        if (_currentContext.Organizations != null && _currentContext.Organizations.Any(o => o.Id == _ssoConfig.OrganizationId))
        {
            // TDE requires single org so grabbing first org & id is fine.
            hasManageResetPasswordPermission = await _currentContext.ManageResetPassword(_ssoConfig!.OrganizationId);
        }

        var hasAdminApproval = false;
        if (_user != null)
        {
            // If sso configuration data is not null then I know for sure that ssoConfiguration isn't null
            var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(_ssoConfig.OrganizationId, _user.Id);

            hasManageResetPasswordPermission |= organizationUser != null && (organizationUser.Type == OrganizationUserType.Owner || organizationUser.Type == OrganizationUserType.Admin);
            // They are only able to be approved by an admin if they have enrolled is reset password
            hasAdminApproval = organizationUser != null && !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);
        }

        _options.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
            hasAdminApproval,
            hasLoginApprovingDevice,
            hasManageResetPasswordPermission,
            isTdeOffboarding,
            encryptedPrivateKey,
            encryptedUserKey);
    }
}

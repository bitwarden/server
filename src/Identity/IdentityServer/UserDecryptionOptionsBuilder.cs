using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Identity.Utilities;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using System.Security.Claims;
using Bit.Core.Auth.Utilities;
using Amazon.Util;

namespace Bit.Identity.IdentityServer;

#nullable enable
/// <summary>
/// Used to create a list of all possible ways the newly authenticated user can decrypt their vault contents
/// </summary>
public class UserDecryptionOptionsBuilder
{
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;

    private UserDecryptionOptions _options = new UserDecryptionOptions();
    private Core.Auth.Entities.SsoConfig? _ssoConfig;
    private Device? _device;

    public UserDecryptionOptionsBuilder(
        ICurrentContext currentContext,
        IFeatureService featureService
    )
    {
        _currentContext = currentContext;
        _featureService = featureService;
    }

    public UserDecryptionOptionsBuilder ForUser(User user)
    {
        _options.HasMasterPassword = user.HasMasterPassword();
        return this;
    }

    public UserDecryptionOptionsBuilder WithSso(Core.Auth.Entities.SsoConfig ssoConfig)
    {
        _ssoConfig = ssoConfig;
        return this;
    }

    public UserDecryptionOptionsBuilder WithDevice(Device device)
    {
        _device = device;
        return this;
    }

    public UserDecryptionOptions Build()
    {
        BuildKeyConnectorOptions();
        BuildTrustedDeviceOptions();

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

    private void BuildTrustedDeviceOptions()
    {
        if (_device == null || _ssoConfig == null || !_featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext))
        {
            return;
        }

        // TODO: Only add the trusted device specific option when the flag is turned on

        var ssoConfigurationData = _ssoConfig.GetData();
        if (ssoConfigurationData is not { MemberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption })
        {
            return;
        }

        
        string? encryptedPrivateKey = null;
        string? encryptedUserKey = null;
        if (_device.IsTrusted())
        {
            encryptedPrivateKey = _device.EncryptedPrivateKey;
            encryptedUserKey = _device.EncryptedUserKey;
        }

        _options.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
            false,
            false,
            false,
            encryptedPrivateKey,
            encryptedUserKey);

        //var allDevices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
        //// Checks if the current user has any devices that are capable of approving login with device requests except for
        //// their current device.
        //// NOTE: this doesn't check for if the users have configured the devices to be capable of approving requests as that is a client side setting.
        //var hasLoginApprovingDevice = allDevices
        //    .Where(d => d.Identifier != device.Identifier && LoginApprovingDeviceTypes.Types.Contains(d.Type))
        //    .Any();

        //// Determine if user has manage reset password permission as post sso logic requires it for forcing users with this permission to set a MP
        //var hasManageResetPasswordPermission = false;

        //// when a user is being created via JIT provisioning, they will not have any orgs so we can't assume we will have orgs here
        //if (CurrentContext.Organizations.Any(o => o.Id == ssoConfiguration!.OrganizationId))
        //{
        //    // TDE requires single org so grabbing first org & id is fine.
        //    hasManageResetPasswordPermission = await CurrentContext.ManageResetPassword(ssoConfiguration!.OrganizationId);
        //}

        //// If sso configuration data is not null then I know for sure that ssoConfiguration isn't null
        //var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(ssoConfiguration!.OrganizationId, user.Id);

        //// They are only able to be approved by an admin if they have enrolled is reset password
        //var hasAdminApproval = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);

        //// TrustedDeviceEncryption only exists for SSO, but if that ever changes this value won't always be true
        //_options.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
        //    hasAdminApproval,
        //    hasLoginApprovingDevice,
        //    hasManageResetPasswordPermission,
        //    encryptedPrivateKey,
        //    encryptedUserKey);
    }
}
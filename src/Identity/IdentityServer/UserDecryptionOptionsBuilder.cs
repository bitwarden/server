using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Identity.Utilities;
using System.Security.Claims;

namespace Bit.Identity.IdentityServer;

/// <summary>
/// Used to create a list of all possible ways the newly authenticated user can decrypt their vault contents
/// </summary>
public class UserDecryptionOptionsBuilder
{
    private readonly UserDecryptionOptions _options = new UserDecryptionOptions();

    public UserDecryptionOptionsBuilder()
    {
    }

    public UserDecryptionOptionsBuilder ForUser(User user)
    {
        _options.HasMasterPassword = user.HasMasterPassword();
        return this;
    }

    public UserDecryptionOptionsBuilder WithSso(SsoConfig ssoConfig)
    {
        var ssoConfigurationData = ssoConfig.GetData();
        if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } && !string.IsNullOrEmpty(ssoConfigurationData.KeyConnectorUrl))
        {
            _options.KeyConnectorOption = new KeyConnectorUserDecryptionOption(ssoConfigurationData.KeyConnectorUrl);
        }
        return this;
    }

    public UserDecryptionOptions Build()
    {
        return _options;
    }

    //private async Task<UserDecryptionOptions> CreateUserDecryptionOptionsAsync(User user, Device device, ClaimsPrincipal subject)
    //{
    //    var ssoConfiguration = await GetSsoConfigurationDataAsync(subject);

    //    var userDecryptionOption = new UserDecryptionOptions
    //    {
    //        HasMasterPassword = !string.IsNullOrEmpty(user.MasterPassword)
    //    };

    //    var ssoConfigurationData = ssoConfiguration?.GetData();

    //    if (ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.KeyConnector } && !string.IsNullOrEmpty(ssoConfigurationData.KeyConnectorUrl))
    //    {
    //        // KeyConnector makes it mutually exclusive
    //        userDecryptionOption.KeyConnectorOption = new KeyConnectorUserDecryptionOption(ssoConfigurationData.KeyConnectorUrl);
    //        return userDecryptionOption;
    //    }

    //    // Only add the trusted device specific option when the flag is turned on
    //    if (FeatureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, CurrentContext) && ssoConfigurationData is { MemberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption })
    //    {
    //        string? encryptedPrivateKey = null;
    //        string? encryptedUserKey = null;
    //        if (device.IsTrusted())
    //        {
    //            encryptedPrivateKey = device.EncryptedPrivateKey;
    //            encryptedUserKey = device.EncryptedUserKey;
    //        }

    //        var allDevices = await _deviceRepository.GetManyByUserIdAsync(user.Id);
    //        // Checks if the current user has any devices that are capable of approving login with device requests except for
    //        // their current device.
    //        // NOTE: this doesn't check for if the users have configured the devices to be capable of approving requests as that is a client side setting.
    //        var hasLoginApprovingDevice = allDevices
    //            .Where(d => d.Identifier != device.Identifier && LoginApprovingDeviceTypes.Types.Contains(d.Type))
    //            .Any();

    //        // Determine if user has manage reset password permission as post sso logic requires it for forcing users with this permission to set a MP
    //        var hasManageResetPasswordPermission = false;

    //        // when a user is being created via JIT provisioning, they will not have any orgs so we can't assume we will have orgs here
    //        if (CurrentContext.Organizations.Any(o => o.Id == ssoConfiguration!.OrganizationId))
    //        {
    //            // TDE requires single org so grabbing first org & id is fine.
    //            hasManageResetPasswordPermission = await CurrentContext.ManageResetPassword(ssoConfiguration!.OrganizationId);
    //        }

    //        // If sso configuration data is not null then I know for sure that ssoConfiguration isn't null
    //        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(ssoConfiguration!.OrganizationId, user.Id);

    //        // They are only able to be approved by an admin if they have enrolled is reset password
    //        var hasAdminApproval = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);

    //        // TrustedDeviceEncryption only exists for SSO, but if that ever changes this value won't always be true
    //        userDecryptionOption.TrustedDeviceOption = new TrustedDeviceUserDecryptionOption(
    //            hasAdminApproval,
    //            hasLoginApprovingDevice,
    //            hasManageResetPasswordPermission,
    //            encryptedPrivateKey,
    //            encryptedUserKey);
    //    }

    //    return userDecryptionOption;
    //}

    //private async Task<SsoConfig?> GetSsoConfigurationDataAsync(ClaimsPrincipal subject)
    //{
    //    var organizationClaim = subject?.FindFirstValue("organizationId");

    //    if (organizationClaim == null || !Guid.TryParse(organizationClaim, out var organizationId))
    //    {
    //        return null;
    //    }

    //    var ssoConfig = await SsoConfigRepository.GetByOrganizationIdAsync(organizationId);
    //    if (ssoConfig == null)
    //    {
    //        return null;
    //    }

    //    return ssoConfig;
    //}
}
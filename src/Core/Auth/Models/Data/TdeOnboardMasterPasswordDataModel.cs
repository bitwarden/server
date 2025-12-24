using Bit.Core.Auth.UserFeatures.TdeOnboardingPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.Models.Data;

/// <summary>
/// Data model for setting a master password for a TDE user.
/// See <see cref="ITdeOnboardingPasswordCommand"/> for more details.
/// </summary>
public class TdeOnboardMasterPasswordDataModel
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }

    /// <summary>
    /// Organization SSO identifier.
    /// </summary>
    public required string OrgSsoIdentifier { get; set; }

    public string? MasterPasswordHint { get; set; }
}

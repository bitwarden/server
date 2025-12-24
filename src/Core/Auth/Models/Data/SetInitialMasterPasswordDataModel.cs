using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.Models.Data;

/// <summary>
/// Data model for setting an initial master password for a user.
/// See <see cref="ISetInitialMasterPasswordCommand"/> for more details.
/// </summary>
public class SetInitialMasterPasswordDataModel
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }

    /// <summary>
    /// Organization SSO identifier.
    /// </summary>
    public required string OrgSsoIdentifier { get; set; }

    public required UserAccountKeysData AccountKeys { get; set; }
    public string? MasterPasswordHint { get; set; }
}

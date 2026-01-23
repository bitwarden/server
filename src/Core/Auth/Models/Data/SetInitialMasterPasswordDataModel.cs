using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.Models.Data;

/// <summary>
/// Data model for setting an initial master password for a user.
/// </summary>
public class SetInitialMasterPasswordDataModel
{
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }

    /// <summary>
    /// Organization SSO identifier.
    /// </summary>
    public required string OrgSsoIdentifier { get; set; }

    /// <summary>
    /// User account keys. Required for Master Password decryption user.
    /// </summary>
    public required UserAccountKeysData? AccountKeys { get; set; }
    public string? MasterPasswordHint { get; set; }
}

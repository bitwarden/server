
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

public class RotateUserAccountKeysData
{
    // Authentication for this requests
    public required string OldMasterKeyAuthenticationHash { get; set; }

    public required UserAccountKeysData AccountKeys { get; set; }

    // All methods to get to the userkey
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public required IEnumerable<EmergencyAccess> EmergencyAccesses { get; set; }
    public required IReadOnlyList<OrganizationUser> OrganizationUsers { get; set; }
    public required IEnumerable<WebAuthnLoginRotateKeyData> WebAuthnKeys { get; set; }
    public required IEnumerable<Device> DeviceKeys { get; set; }

    // User vault data encrypted by the userkey
    public required IEnumerable<Cipher> Ciphers { get; set; }
    public required IEnumerable<Folder> Folders { get; set; }
    public required IReadOnlyList<Send> Sends { get; set; }
}

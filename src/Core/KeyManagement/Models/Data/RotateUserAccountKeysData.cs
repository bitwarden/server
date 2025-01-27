using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

public class RotateUserAccountKeysData
{
    // Authentication for this requests
    public string OldMasterkeyAuthenticationHash { get; set; }

    // Other keys encrypted by the userkey
    public string UserKeyEncryptedAccountPrivateKey { get; set; }
    public string AccountPublicKey { get; set; }

    // All methods to get to the userkey
    public MasterPasswordUnlockData MasterPasswordUnlockData { get; set; }
    public IEnumerable<EmergencyAccess> EmergencyAccesses { get; set; }
    public IReadOnlyList<OrganizationUser> OrganizationUsers { get; set; }
    public IEnumerable<WebAuthnLoginRotateKeyData> WebAuthnKeys { get; set; }

    // User vault data encrypted by the userkey
    public IEnumerable<Cipher> Ciphers { get; set; }
    public IEnumerable<Folder> Folders { get; set; }
    public IReadOnlyList<Send> Sends { get; set; }
}

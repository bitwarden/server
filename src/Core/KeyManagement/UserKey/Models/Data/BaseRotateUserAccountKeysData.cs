using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class BaseRotateUserAccountKeysData
{
    public required UserAccountKeysData AccountKeys { get; set; }

    // Common methods to get the userKey
    public required IEnumerable<EmergencyAccess> EmergencyAccesses { get; set; }
    public required IReadOnlyList<OrganizationUser> OrganizationUsers { get; set; }
    public required IEnumerable<WebAuthnLoginRotateKeyData> WebAuthnKeys { get; set; }
    public required IEnumerable<Device> DeviceKeys { get; set; }
    public V2UpgradeTokenData? V2UpgradeToken { get; set; }

    // User vault data encrypted by the userKey
    public required IEnumerable<Cipher> Ciphers { get; set; }
    public required IEnumerable<Folder> Folders { get; set; }
    public required IReadOnlyList<Send> Sends { get; set; }
}

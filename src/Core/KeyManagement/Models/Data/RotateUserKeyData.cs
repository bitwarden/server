using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.KeyManagement.Models.Data;

public class RotateUserKeyData
{
    public string MasterPasswordHash { get; set; }
    public string Key { get; set; }
    public string PrivateKey { get; set; }
    public IEnumerable<Cipher> Ciphers { get; set; }
    public IEnumerable<Folder> Folders { get; set; }
    public IReadOnlyList<Send> Sends { get; set; }
    public IEnumerable<EmergencyAccess> EmergencyAccesses { get; set; }
    public IReadOnlyList<OrganizationUser> OrganizationUsers { get; set; }
    public IEnumerable<WebAuthnLoginRotateKeyData> WebAuthnKeys { get; set; }
}

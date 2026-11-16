using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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
    public KeyId? OldUserKeyId { get; set; }
    public KeyId? NewUserKeyId { get; set; }

    // User vault data encrypted by the userKey
    public required IEnumerable<Cipher> Ciphers { get; set; }
    public required IEnumerable<Folder> Folders { get; set; }
    public required IReadOnlyList<Send> Sends { get; set; }

    /// <summary>
    /// Rejects a rotation from a client that holds an out-of-date user key. The ids are compared only when
    /// both are present: the user may have no key id recorded yet, and the client may predate the field.
    /// </summary>
    /// <exception cref="ConflictException">The client must sync to get the current user key.</exception>
    public void ValidateOldUserKeyIdForUser(User user)
    {
        var currentUserKeyId = user.GetUserKeyId();
        if (currentUserKeyId is null || OldUserKeyId is null)
        {
            return;
        }

        if (!currentUserKeyId.Equals(OldUserKeyId))
        {
            throw new ConflictException("The user key being rotated is not the current user key. Sync and try again.");
        }
    }
}

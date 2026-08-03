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

    // User vault data encrypted by the userKey
    public required IEnumerable<Cipher> Ciphers { get; set; }
    public required IEnumerable<Folder> Folders { get; set; }
    public required IReadOnlyList<Send> Sends { get; set; }

    /// <summary>
    /// Key id of the new user key this rotation sets, when the client supplied it. This is the
    /// authoritative key id of the request: it is the value persisted as the account's key id.
    /// </summary>
    public KeyId? UserKeyId { get; set; }

    /// <summary>
    /// Validates the provided key id against the key id of this request. This should be used to verify
    /// that the items directly encrypted by the user-key (cipher-keys, private-key, signature-key) are
    /// encrypted by the correct key.
    /// </summary>
    public void ValidateContainedKeyIdMatches(KeyId? containedKeyId)
    {
        if (containedKeyId == null)
        {
            return;
        }

        if (containedKeyId != UserKeyId)
        {
            throw new BadRequestException(
                "The user key id contained in the unlock data must match the user key id of the key rotation.");
        }
    }
}

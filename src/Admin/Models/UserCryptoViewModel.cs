using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;

namespace Bit.Admin.Models;

/// <summary>
/// Encryption indicators for Customer Support. Lets support tell at a glance whether an issue may
/// relate to crypto changes such as the v2 encryption roll-out or kdf upgrades.
/// </summary>
public class UserCryptoViewModel
{
    public string? UserKeyId { get; init; }
    public bool UsesV2Keys { get; init; }
    public bool LastRotationFromV1 { get; init; }
    public bool KeyRotatedWithPasswordChange { get; init; }
    public bool NeedsRecoveryKeyV2Update { get; init; }
    public bool NeedsKdfUpdate { get; init; }

    public static UserCryptoViewModel Map(User user, bool needsRecoveryKeyV2Update) =>
        new()
        {
            UserKeyId = user.UserKeyId,
            UsesV2Keys = user.HasV2Encryption(),
            // Heuristic: the upgrade token only exists after a V1 -> V2 rotation and is cleared by any later rotation.
            LastRotationFromV1 = user.V2UpgradeToken != null,
            KeyRotatedWithPasswordChange = RotatedWithPasswordChange(user),
            NeedsRecoveryKeyV2Update = needsRecoveryKeyV2Update,
            NeedsKdfUpdate = user.Kdf == KdfType.PBKDF2_SHA256 &&
                user.KdfIterations < KdfConstants.PBKDF2_ITERATIONS.Min,
        };

    // A rotation done together with a password change is stamped with the same timestamp.
    private static bool RotatedWithPasswordChange(User user) =>
        user.LastKeyRotationDate != null && user.LastKeyRotationDate == user.LastPasswordChangeDate;
}

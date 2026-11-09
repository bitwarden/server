using Bit.Core.Entities;

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
            LastRotationFromV1 = user.LastRotationFromV1(),
            KeyRotatedWithPasswordChange = user.KeyRotatedWithPassword(),
            NeedsRecoveryKeyV2Update = needsRecoveryKeyV2Update,
            NeedsKdfUpdate = user.NeedsKdfUpdate(),
        };
}

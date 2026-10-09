using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// One sponsored customer of an <see cref="OrganizationPartnership"/>, identified by the partner's
/// external identifier and optionally bound to a Bitwarden account.
/// </summary>
/// <remarks>
/// A partnership holds at most one entitlement per external identifier. Re-provisioning a canceled
/// external identifier reuses the same record, which is what lets it bind to a different account.
/// </remarks>
public class OrganizationPartnershipEntitlement : ITableObject<Guid>
{
    public const int ExternalIdMaxLength = 128;

    public Guid Id { get; set; }
    public Guid OrganizationPartnershipId { get; set; }
    /// <summary>
    /// The partner's opaque customer identifier, at most <see cref="ExternalIdMaxLength"/> characters.
    /// Plaintext in memory; repositories encrypt it at rest, so the stored value is longer.
    /// </summary>
    public string ExternalId { get; set; } = null!;
    /// <summary>
    /// Lookup key for <see cref="ExternalId"/>. See <see cref="ComputeExternalIdHash"/>.
    /// </summary>
    [MaxLength(64)]
    public string ExternalIdHash { get; set; } = null!;
    public PartnershipEntitlementState State { get; set; }
    /// <summary>
    /// The bound account. Held through a cancel until the resume window elapses.
    /// </summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// The opaque binding reference shown to the partner in place of an account identity.
    /// Minted when a binding is established and replaced on re-bind.
    /// </summary>
    public Guid? AccountRef { get; set; }
    /// <summary>
    /// A JSON-serialized flat string map supplied by the partner and echoed on reads.
    /// Use <see cref="GetMetadata"/> and <see cref="SetMetadata"/>.
    /// </summary>
    public string? Metadata { get; set; }
    public DateTime? BoundDate { get; set; }
    public DateTime? SuspendedDate { get; set; }
    public DateTime? CanceledDate { get; set; }
    public DateTime? ResumeWindowExpirationDate { get; set; }
    /// <summary>
    /// The effectiveAt of the last applied transition. Older transitions are acknowledged but not applied.
    /// </summary>
    public DateTime LastAppliedEffectiveDate { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public IDictionary<string, string> GetMetadata() =>
        string.IsNullOrEmpty(Metadata)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(Metadata) ?? new Dictionary<string, string>();

    public void SetMetadata(IDictionary<string, string>? metadata) =>
        Metadata = metadata is null || metadata.Count == 0 ? null : JsonSerializer.Serialize(metadata);

    /// <summary>
    /// Mints a new <see cref="AccountRef"/>. Random rather than COMB so it reveals nothing about when it was made.
    /// </summary>
    public void SetNewAccountRef() => AccountRef = Guid.NewGuid();

    /// <summary>
    /// Hex HMAC-SHA256 of the partnership ID and external identifier under <paramref name="hashKey"/>. Keyed so a
    /// database reader can't recover identifiers by enumeration, and scoped so one customer ID hashes differently
    /// under each partnership.
    /// </summary>
    public static string ComputeExternalIdHash(byte[] hashKey, Guid organizationPartnershipId, string externalId)
    {
        var bytes = Encoding.UTF8.GetBytes($"{organizationPartnershipId:N}:{externalId}");
        return Convert.ToHexString(HMACSHA256.HashData(hashKey, bytes));
    }

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CombGuid.Generate();
        }
    }
}

namespace Bit.Pam.Models;

/// <summary>
/// A cipher or an access rule the audit trail names within a range; exactly one pair is set. Cipher names are vault
/// data the store never holds, so the client resolves them from its own vault.
/// </summary>
public class AccessAuditItem
{
    public Guid? CipherId { get; set; }

    /// <summary>
    /// The collection the cipher was most recently gated through, which tells two items sharing a decrypted name
    /// apart.
    /// </summary>
    public Guid? CollectionId { get; set; }

    public Guid? RuleId { get; set; }

    /// <summary>As the most recent event in range recorded it, so a renamed rule shows its newest name.</summary>
    public string? RuleName { get; set; }
}

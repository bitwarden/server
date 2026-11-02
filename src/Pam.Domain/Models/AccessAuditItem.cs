namespace Bit.Pam.Models;

/// <summary>
/// One subject the access-audit trail names within a range, either a cipher or an access rule. Exactly one of the two
/// pairs is set. A rule's name is plaintext organization configuration and travels with it, while a cipher's name is
/// vault data the store never holds, so the client resolves cipher names from its own vault and drops the ones it
/// cannot read.
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

    /// <summary>
    /// The rule's name as the most recent event in range recorded it, so a renamed rule reads in the menu the way the
    /// newest rows read in the table.
    /// </summary>
    public string? RuleName { get; set; }
}

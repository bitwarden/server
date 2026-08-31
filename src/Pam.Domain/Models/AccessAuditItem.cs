namespace Bit.Pam.Models;

/// <summary>
/// One subject the access-audit trail names within a range — a cipher, or an access rule — as the trail's Item filter
/// offers it.
///
/// Exactly one of the two pairs is set. They are not interchangeable: a rule's name is plaintext organization
/// configuration and travels with it, while a cipher's name is Vault Data the store never holds, so no name is carried
/// for one. The client resolves cipher names from its own vault and drops the ones it cannot read — which is the whole
/// reason this is a list of ids rather than a list of labels.
/// </summary>
public class AccessAuditItem
{
    /// <summary>The subject cipher. Null on a rule item.</summary>
    public Guid? CipherId { get; set; }

    /// <summary>
    /// The collection the cipher was most recently gated through, which is what tells two items sharing a decrypted
    /// name apart. Null on a rule item, or where the events named no collection.
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>The subject access rule. Null on a cipher item.</summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// The rule's name as the most recent event in range recorded it, so a renamed rule reads in the menu the way the
    /// newest rows read in the table. Null on a cipher item.
    /// </summary>
    public string? RuleName { get; set; }
}

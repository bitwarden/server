namespace Bit.Pam.Models;

/// <summary>
/// One subject the access-audit trail names within a range — a cipher, or an access rule — as the trail's Item
/// filter offers it. Exactly one of the two pairs is set. A rule's name travels with it as plaintext organization
/// configuration; a cipher's does not, since it is Vault Data the caller may be unable to decrypt, so the client
/// resolves cipher names itself from its own vault.
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

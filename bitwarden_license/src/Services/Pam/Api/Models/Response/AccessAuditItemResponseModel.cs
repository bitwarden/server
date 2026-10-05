using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// One subject the access-audit trail names within a range, as the trail's Item filter offers it. Exactly one of
/// the two pairs is set, distinguishing a credential from an access rule.
///
/// No cipher name is here: it's Vault Data, so the client resolves it from its own vault instead. A rule's name is
/// plaintext organization configuration, so it travels with the id.
/// </summary>
public class AccessAuditItemResponseModel : ResponseModel
{
    public AccessAuditItemResponseModel(AccessAuditItem item)
        : base("accessAuditItem")
    {
        ArgumentNullException.ThrowIfNull(item);

        CipherId = item.CipherId;
        CollectionId = item.CollectionId;
        RuleId = item.RuleId;
        RuleName = item.RuleName;
    }

    /// <summary>The subject cipher. Null on a rule item.</summary>
    public Guid? CipherId { get; }

    /// <summary>
    /// The collection the cipher was most recently gated through — what tells two items sharing a decrypted name
    /// apart. Null on a rule item, or where the events named no collection.
    /// </summary>
    public Guid? CollectionId { get; }

    /// <summary>The subject access rule. Null on a cipher item.</summary>
    public Guid? RuleId { get; }

    /// <summary>The rule's name as the most recent event in range recorded it. Null on a cipher item.</summary>
    public string? RuleName { get; }
}

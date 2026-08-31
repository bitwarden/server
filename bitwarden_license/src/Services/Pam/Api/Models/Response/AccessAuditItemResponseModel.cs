using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// One subject the access-audit trail names within a range, as the trail's Item filter offers it. Exactly one of the
/// two pairs is set, and which one is how the client tells a credential from an access rule.
///
/// No cipher name is here, deliberately. A cipher's name is Vault Data — an EncString an auditor generally cannot
/// decrypt — so the client resolves names from its own vault and drops the items it cannot label. A rule's name is
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

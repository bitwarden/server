using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// One subject the access-audit trail names within a range, for the Item filter: either a credential or an access
/// rule. Cipher names are vault data, so the client resolves them itself.
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
    /// The collection the cipher was most recently accessed through. Null on a rule item, or where the events named no
    /// collection.
    /// </summary>
    public Guid? CollectionId { get; }

    /// <summary>The subject access rule. Null on a cipher item.</summary>
    public Guid? RuleId { get; }

    /// <summary>The rule's most recently recorded name. Null on a cipher item.</summary>
    public string? RuleName { get; }
}

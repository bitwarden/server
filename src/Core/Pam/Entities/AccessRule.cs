using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Pam.Entities;

/// <summary>
/// A reusable, org-scoped PAM access rule. Referenced by collections (and eventually Secrets Manager
/// entities) via FK to govern credential lease decisions.
/// </summary>
public class AccessRule : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// JSON conditions document (an <c>AccessCondition</c> tree). Validated by <c>AccessRuleValidator</c> before
    /// being persisted.
    /// </summary>
    public string Conditions { get; set; } = null!;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}

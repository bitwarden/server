using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>An organization's PAM access rule, referenced by collections to govern credential leasing.</summary>
public class AccessRule : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// A JSON array of <c>AccessCondition</c> objects, validated by <c>AccessRuleValidator</c> before it is persisted.
    /// </summary>
    public string Conditions { get; set; } = null!;

    /// <summary>
    /// When true, at most one active lease per cipher across all users. Binds a member only when every collection
    /// through which they reach the cipher is governed by a rule with this flag set.
    /// </summary>
    public bool SingleActiveLease { get; set; }

    /// <summary>Pre-fills the duration of a request under this rule. Null means the global default.</summary>
    public int? DefaultLeaseDurationSeconds { get; set; }

    /// <summary>Ceiling on any single lease under this rule. Null means only the global maximum applies.</summary>
    public int? MaxLeaseDurationSeconds { get; set; }

    /// <summary>When false, the rule does not gate access to its collections.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, an active lease under this rule may be extended once, without human approval.</summary>
    public bool AllowsExtensions { get; set; }

    /// <summary>Must be positive when <see cref="AllowsExtensions"/> is true; ignored otherwise.</summary>
    public int? MaxExtensionDurationSeconds { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>The user who created or last updated the rule.</summary>
    public Guid? LastEditedBy { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}

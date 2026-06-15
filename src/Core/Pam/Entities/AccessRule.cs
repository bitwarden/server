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

    /// <summary>
    /// When true, the rule asks for a per-cipher singleton: at most one active lease may exist for a given cipher
    /// across all users. The constraint binds for a member only when every collection through which they reach the
    /// cipher is governed by a rule with this flag set; any ungated or non-singleton path is an escape that leaves
    /// the member unconstrained.
    /// </summary>
    public bool SingleActiveLease { get; set; }

    /// <summary>
    /// Default lease duration in seconds, used to pre-fill a request opened under this rule. Null means no
    /// rule-specific default is stored and the backend default applies.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; set; }

    /// <summary>
    /// Hard ceiling on the duration of any single lease granted under this rule, in seconds. Null means no
    /// per-rule cap (the global maximum still applies).
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; set; }

    /// <summary>
    /// When false, the rule is inactive: it does not gate access for the collections it governs. New rules
    /// default to enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it. Extensions are always
    /// auto-approved (regardless of the rule's approval conditions), up to <see cref="MaxExtensions"/> times.
    /// </summary>
    public bool AllowsExtensions { get; set; }

    /// <summary>
    /// The maximum number of times a single lease granted under this rule may be extended. Required to be a
    /// positive value when <see cref="AllowsExtensions"/> is true; ignored otherwise.
    /// </summary>
    public int? MaxExtensions { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}

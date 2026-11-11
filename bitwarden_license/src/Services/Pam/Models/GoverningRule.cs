using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Models;

/// <summary>
/// The access rule that governs a cipher for a particular caller. A null governing rule means the cipher is not
/// leasing-gated for the caller.
/// </summary>
public sealed record GoverningRule(
    Guid OrganizationId,
    Guid CollectionId,
    bool RequiresHumanApproval,
    IReadOnlyList<AccessCondition> Conditions)
{
    /// <summary>The resolved rule's id, pinned on a request at submit so later operations use the same rule.</summary>
    public Guid RuleId { get; init; }

    /// <summary>
    /// When true, a lease under this rule can be extended once, approved automatically, by up to
    /// <see cref="MaxExtensionDurationSeconds"/>.
    /// </summary>
    public bool AllowsExtensions { get; init; }

    /// <summary>
    /// The longest single extension in seconds; meaningful only when <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; init; }

    /// <summary>
    /// The rule's default lease duration in seconds, or null for the global default. Read it through
    /// <see cref="LeaseDurationBounds"/>.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; init; }

    /// <summary>
    /// The rule's cap on a single lease in seconds, or null for the global cap alone. Read it through
    /// <see cref="LeaseDurationBounds"/>.
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; init; }

    public IReadOnlyList<AccessCondition> AutomatedConditions =>
        Conditions.Where(condition => condition is not HumanApprovalCondition).ToList();

    /// <summary>
    /// True if the stored conditions could not be parsed and <see cref="Conditions"/> holds the resolver's
    /// fail-safe stand-in. A caller evaluating <see cref="AutomatedConditions"/> must refuse.
    /// </summary>
    public bool ConditionsUnreadable { get; init; }
}

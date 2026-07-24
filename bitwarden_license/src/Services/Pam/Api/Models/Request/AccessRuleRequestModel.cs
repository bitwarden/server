using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Api.Models.Request;

public class AccessRuleRequestModel
{
    /// <summary>
    /// The rule's display name, shown wherever rules are listed and managed. Required; up to 256 characters.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Optional free-text describing the rule's intent. Has no effect on evaluation; surfaced to admins only.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When false, the rule is inactive and does not gate access for the collections it governs. Defaults to
    /// true so a request that omits the field creates an active rule.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The condition tree that decides how access is granted under this rule — for example requiring human
    /// approval, or restricting to certain times of day or source IPs. Sent as a JSON object and stored verbatim;
    /// an empty or null value means the rule imposes no conditions.
    /// </summary>
    [Required]
    public object Conditions { get; set; } = null!;

    /// <summary>
    /// When true, the rule enforces a per-cipher singleton (at most one active lease per cipher across all users).
    /// </summary>
    public bool SingleActiveLease { get; set; }

    /// <summary>
    /// Default lease duration in seconds, used to pre-fill a request opened under this rule. Null means the
    /// backend default applies.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; set; }

    /// <summary>
    /// Hard ceiling on the duration of any single lease granted under this rule, in seconds. Null means no
    /// per-rule cap.
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; set; }

    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it once (always auto-approved), by up
    /// to <see cref="MaxExtensionDurationSeconds"/>.
    /// </summary>
    public bool AllowsExtensions { get; set; }

    /// <summary>
    /// The longest a single extension may run, in seconds. Required to be positive when
    /// <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; set; }

    /// <summary>
    /// The complete set of collections this rule governs. The rule's associations are replaced to match
    /// exactly this set; an empty array clears all associations.
    /// </summary>
    [Required]
    public IEnumerable<Guid> Collections { get; set; } = null!;
}

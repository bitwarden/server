using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Pam.Entities;

namespace Bit.Api.Pam.Models.Request;

public class AccessRuleRequestModel
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

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
    /// When false, the rule is inactive and does not gate access for the collections it governs. Defaults to
    /// true so a request that omits the field creates an active rule.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it (always auto-approved), up to
    /// <see cref="MaxExtensions"/> times.
    /// </summary>
    public bool AllowsExtensions { get; set; }

    /// <summary>
    /// The maximum number of times a single lease may be extended. Required to be positive when
    /// <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensions { get; set; }

    /// <summary>
    /// The complete set of collections this rule governs. The rule's associations are replaced to match
    /// exactly this set; an empty array clears all associations.
    /// </summary>
    [Required]
    public IEnumerable<Guid> Collections { get; set; } = null!;

    public AccessRule ToAccessRule(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        Description = Description,
        Conditions = SerializeConditions(Conditions),
        SingleActiveLease = SingleActiveLease,
        DefaultLeaseDurationSeconds = DefaultLeaseDurationSeconds,
        MaxLeaseDurationSeconds = MaxLeaseDurationSeconds,
        Enabled = Enabled,
        AllowsExtensions = AllowsExtensions,
        MaxExtensions = MaxExtensions,
    };

    private static string SerializeConditions(object conditions) => conditions switch
    {
        JsonElement je when je.ValueKind == JsonValueKind.Null => string.Empty,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(conditions),
    };
}

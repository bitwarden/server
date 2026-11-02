using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.Api.Models.Request;

public class AccessRuleRequestModel
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    /// <summary>Free text describing the rule's intent; has no effect on evaluation.</summary>
    public string? Description { get; set; }

    /// <summary>When false, the rule does not gate access to its collections. Defaults to true when omitted.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// A JSON array of condition objects, such as human approval or a source IP restriction, stored verbatim. An
    /// empty array means requests under the rule resolve automatically.
    /// </summary>
    [Required]
    public object Conditions { get; set; } = null!;

    /// <summary>When true, at most one active lease per cipher across all users.</summary>
    public bool SingleActiveLease { get; set; }

    /// <summary>Pre-fills the duration of a request under this rule. Null means the global default.</summary>
    public int? DefaultLeaseDurationSeconds { get; set; }

    /// <summary>Ceiling on any single lease under this rule. Null means no per-rule cap.</summary>
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
    /// The complete set of collections this rule governs, replacing its current ones. An empty array clears them.
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
        MaxExtensionDurationSeconds = MaxExtensionDurationSeconds,
    };

    private static string SerializeConditions(object conditions) => conditions switch
    {
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(conditions),
    };
}

using System.Text.Json;
using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

public class AccessRuleResponseModel : ResponseModel
{
    public AccessRuleResponseModel(AccessRuleDetails rule)
        : base("accessRule")
    {
        ArgumentNullException.ThrowIfNull(rule);

        Id = rule.Id;
        OrganizationId = rule.OrganizationId;
        Name = rule.Name;
        Description = rule.Description;
        Enabled = rule.Enabled;
        Conditions = TryParseConditions(rule.Conditions);
        SingleActiveLease = rule.SingleActiveLease;
        DefaultLeaseDurationSeconds = rule.DefaultLeaseDurationSeconds;
        MaxLeaseDurationSeconds = rule.MaxLeaseDurationSeconds;
        AllowsExtensions = rule.AllowsExtensions;
        MaxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
        Collections = rule.CollectionIds.ToList();
        CreationDate = rule.CreationDate.AsUtc();
        RevisionDate = rule.RevisionDate.AsUtc();
    }

    /// <summary>
    /// The rule's unique identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The organization this rule belongs to.
    /// </summary>
    public Guid OrganizationId { get; }

    /// <summary>
    /// The rule's display name, shown wherever rules are listed and managed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional free-text describing the rule's intent. Has no effect on evaluation; surfaced to admins only.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// When false, the rule is inactive and does not gate access for the collections it governs.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// The conditions that decide how access is granted under this rule — for example requiring human
    /// approval, or restricting to certain times of day or source IPs. Returned as a JSON array of condition
    /// objects; an empty array (or null) means the rule imposes no conditions.
    /// </summary>
    public JsonElement? Conditions { get; }

    /// <summary>
    /// When true, the rule enforces a per-cipher singleton (at most one active lease per cipher across all users).
    /// </summary>
    public bool SingleActiveLease { get; }

    /// <summary>
    /// Default lease duration in seconds, used to pre-fill a request opened under this rule. Null means the
    /// backend default applies.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; }

    /// <summary>
    /// Hard ceiling on the duration of any single lease granted under this rule, in seconds. Null means no
    /// per-rule cap.
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; }

    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it once (always auto-approved), by up
    /// to <see cref="MaxExtensionDurationSeconds"/>.
    /// </summary>
    public bool AllowsExtensions { get; }

    /// <summary>
    /// The longest a single extension may run, in seconds. Set when <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; }

    /// <summary>
    /// The complete set of collections this rule governs.
    /// </summary>
    public IEnumerable<Guid> Collections { get; }

    /// <summary>
    /// When the rule was created (UTC).
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// When the rule was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; }

    private static JsonElement? TryParseConditions(string? conditionsJson)
    {
        if (string.IsNullOrEmpty(conditionsJson))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(conditionsJson).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

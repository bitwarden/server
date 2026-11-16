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

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public string Name { get; }

    /// <summary>Free text describing the rule's intent; has no effect on evaluation.</summary>
    public string? Description { get; }

    /// <summary>When false, the rule does not gate access to its collections.</summary>
    public bool Enabled { get; }

    /// <summary>
    /// A JSON array of condition objects, such as human approval or a source IP restriction. An empty array means the
    /// rule imposes no conditions.
    /// </summary>
    public JsonElement? Conditions { get; }

    /// <summary>When true, at most one active lease per cipher across all users.</summary>
    public bool SingleActiveLease { get; }

    /// <summary>Pre-fills the duration of a request under this rule. Null means the global default.</summary>
    public int? DefaultLeaseDurationSeconds { get; }

    /// <summary>Ceiling on any single lease under this rule. Null means no per-rule cap.</summary>
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

    public IEnumerable<Guid> Collections { get; }

    public DateTime CreationDate { get; }

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

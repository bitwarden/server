using System.Text.Json;
using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Commercial.Pam.Api.Models.Response;

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
        Conditions = TryParseConditions(rule.Conditions);
        SingleActiveLease = rule.SingleActiveLease;
        DefaultLeaseDurationSeconds = rule.DefaultLeaseDurationSeconds;
        MaxLeaseDurationSeconds = rule.MaxLeaseDurationSeconds;
        Enabled = rule.Enabled;
        AllowsExtensions = rule.AllowsExtensions;
        MaxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
        CreationDate = rule.CreationDate.AsUtc();
        RevisionDate = rule.RevisionDate.AsUtc();
        Collections = rule.CollectionIds.ToList();
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public string? Description { get; }
    public JsonElement? Conditions { get; }
    public bool SingleActiveLease { get; }
    public int? DefaultLeaseDurationSeconds { get; }
    public int? MaxLeaseDurationSeconds { get; }
    public bool Enabled { get; }
    public bool AllowsExtensions { get; }
    public int? MaxExtensionDurationSeconds { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }
    public IEnumerable<Guid> Collections { get; }

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

using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

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
        CreationDate = rule.CreationDate;
        RevisionDate = rule.RevisionDate;
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

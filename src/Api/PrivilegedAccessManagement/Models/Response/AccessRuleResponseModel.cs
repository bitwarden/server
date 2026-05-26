using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Api.PrivilegedAccessManagement.Models.Response;

public class AccessRuleResponseModel : ResponseModel
{
    public AccessRuleResponseModel(AccessRule rule)
        : base("accessRule")
    {
        ArgumentNullException.ThrowIfNull(rule);

        Id = rule.Id;
        OrganizationId = rule.OrganizationId;
        Name = rule.Name;
        Description = rule.Description;
        Rule = TryParseRule(rule.Rule);
        CreationDate = rule.CreationDate;
        RevisionDate = rule.RevisionDate;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public string? Description { get; }
    public JsonElement? Rule { get; }
    public DateTime CreationDate { get; }
    public DateTime RevisionDate { get; }

    private static JsonElement? TryParseRule(string? ruleJson)
    {
        if (string.IsNullOrEmpty(ruleJson))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(ruleJson).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

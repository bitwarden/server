using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public class IntegrationFilterService : IIntegrationFilterService
{
    private readonly Dictionary<string, IntegrationFilter> _equalsFilters = new();
    private readonly Dictionary<string, IntegrationFilter> _inFilters = new();
    private static readonly string[] _filterableProperties = new[]
    {
        "UserId",
        "InstallationId",
        "ProviderId",
        "CipherId",
        "CollectionId",
        "GroupId",
        "PolicyId",
        "OrganizationUserId",
        "ProviderUserId",
        "ProviderOrganizationId",
        "ActingUserId",
        "SecretId",
        "ServiceAccountId"
    };

    public IntegrationFilterService()
    {
        BuildFilters();
    }

    public bool EvaluateFilterGroup(IntegrationFilterGroup group, EventMessage message)
    {
        var ruleResults = group.Rules?.Select(
            rule => EvaluateRule(rule, message)
        ) ?? Enumerable.Empty<bool>();
        var groupResults = group.Groups?.Select(
            innerGroup => EvaluateFilterGroup(innerGroup, message)
        ) ?? Enumerable.Empty<bool>();

        var results = ruleResults.Concat(groupResults);
        return group.AndOperator ? results.All(r => r) : results.Any(r => r);
    }

    private bool EvaluateRule(IntegrationFilterRule rule, EventMessage message)
    {
        var key = rule.Property;
        return rule.Operation switch
        {
            IntegrationFilterOperation.Equals => _equalsFilters.TryGetValue(key, out var equals) &&
                                                 equals(message, ToGuid(rule.Value)),
            IntegrationFilterOperation.NotEquals => !(_equalsFilters.TryGetValue(key, out var equals) &&
                                                 equals(message, ToGuid(rule.Value))),
            IntegrationFilterOperation.In => _inFilters.TryGetValue(key, out var inList) &&
                                             inList(message, ToGuidList(rule.Value)),
            IntegrationFilterOperation.NotIn => !(_inFilters.TryGetValue(key, out var inList) &&
                                                inList(message, ToGuidList(rule.Value))),
            _ => false
        };
    }

    private void BuildFilters()
    {
        foreach (var property in _filterableProperties)
        {
            _equalsFilters[property] = IntegrationFilterFactory.BuildEqualityFilter<Guid?>(property);
            _inFilters[property] = IntegrationFilterFactory.BuildInFilter<Guid?>(property);
        }
    }

    private static Guid? ToGuid(object? value)
    {
        if (value is Guid guid)
        {
            return guid;
        }
        if (value is string stringValue)
        {
            return Guid.Parse(stringValue);
        }
        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetGuid();
        }

        throw new InvalidCastException("Could not convert value to Guid");
    }

    private static IEnumerable<Guid?> ToGuidList(object? value)
    {
        if (value is IEnumerable<Guid?> guidList)
        {
            return guidList;
        }
        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonElement)
        {
            var list = new List<Guid?>();
            foreach (var item in jsonElement.EnumerateArray())
            {
                list.Add(ToGuid(item));
            }
            return list;
        }

        throw new InvalidCastException("Could not convert value to Guid[]");
    }
}

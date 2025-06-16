#nullable enable

using System.Linq.Expressions;
using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public class IntegrationFilterService : IIntegrationFilterService
{
    private delegate bool CompiledFilter(EventMessage message, object? value);

    private readonly Dictionary<string, CompiledFilter> _equalsFilters = new();
    private readonly Dictionary<string, CompiledFilter> _inFilters = new();
    private readonly Dictionary<string, CompiledFilter> _dateBeforeFilters = new();
    private readonly Dictionary<string, CompiledFilter> _dateAfterFilters = new();

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
            IntegrationFilterOperation.DateBefore => _dateBeforeFilters.TryGetValue(key, out var dateBefore) &&
                                                     dateBefore(message, ToDateTime(rule.Value)),
            IntegrationFilterOperation.DateAfter => _dateAfterFilters.TryGetValue(key, out var dateAfter) &&
                                                    dateAfter(message, ToDateTime(rule.Value)),
            _ => false
        };
    }

    private void BuildFilters()
    {
        AddEqualityFilter<Guid?>("UserId");
        AddEqualityFilter<Guid?>("InstallationId");
        AddEqualityFilter<Guid?>("ProviderId");
        AddEqualityFilter<Guid?>("CipherId");
        AddEqualityFilter<Guid?>("CollectionId");
        AddEqualityFilter<Guid?>("GroupId");
        AddEqualityFilter<Guid?>("PolicyId");
        AddEqualityFilter<Guid?>("OrganizationUserId");
        AddEqualityFilter<Guid?>("ProviderUserId");
        AddEqualityFilter<Guid?>("ProviderOrganizationId");
        AddEqualityFilter<Guid?>("ActingUserId");
        AddEqualityFilter<Guid?>("SecretId");
        AddEqualityFilter<Guid?>("ServiceAccountId");

        AddInFilter<Guid?>("UserId");
        AddInFilter<Guid?>("InstallationId");
        AddInFilter<Guid?>("ProviderId");
        AddInFilter<Guid?>("CipherId");
        AddInFilter<Guid?>("CollectionId");
        AddInFilter<Guid?>("GroupId");
        AddInFilter<Guid?>("PolicyId");
        AddInFilter<Guid?>("OrganizationUserId");
        AddInFilter<Guid?>("ProviderUserId");
        AddInFilter<Guid?>("ProviderOrganizationId");
        AddInFilter<Guid?>("ActingUserId");
        AddInFilter<Guid?>("SecretId");
        AddInFilter<Guid?>("ServiceAccountId");

        AddDateFilter("Date", before: true);
        AddDateFilter("Date", before: false);
    }
    private void AddEqualityFilter<T>(string propertyName)
    {
        var param = Expression.Parameter(typeof(EventMessage), "m");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var property = Expression.PropertyOrField(param, propertyName);
        var typedVal = Expression.Convert(valueParam, typeof(T));
        var body = Expression.Equal(property, typedVal);

        var lambda = Expression.Lambda<Func<EventMessage, object?, bool>>(body, param, valueParam);
        _equalsFilters[propertyName] = new CompiledFilter(lambda.Compile());
    }

    private void AddInFilter<T>(string propertyName)
    {
        var param = Expression.Parameter(typeof(EventMessage), "m");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var property = Expression.PropertyOrField(param, propertyName);

        var method = typeof(Enumerable)
            .GetMethods()
            .FirstOrDefault(m =>
                m.Name == "Contains"
                && m.GetParameters().Length == 2)
            ?.MakeGenericMethod(typeof(T));
        if (method is null)
        {
            throw new InvalidOperationException("Could not find Contains method.");
        }

        var listType = typeof(IEnumerable<T>);
        var castedList = Expression.Convert(valueParam, listType);

        var containsCall = Expression.Call(method, castedList, property);

        var lambda = Expression.Lambda<Func<EventMessage, object?, bool>>(containsCall, param, valueParam);
        _inFilters[propertyName] = new CompiledFilter(lambda.Compile());
    }

    private void AddDateFilter(string propertyName, bool before)
    {
        var param = Expression.Parameter(typeof(EventMessage), "m");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var property = Expression.PropertyOrField(param, propertyName); // DateTime
        var typedVal = Expression.Convert(valueParam, typeof(DateTime));

        var comparison = before
            ? Expression.LessThan(property, typedVal)
            : Expression.GreaterThan(property, typedVal);

        var lambda = Expression.Lambda<Func<EventMessage, object?, bool>>(comparison, param, valueParam);

        if (before)
            _dateBeforeFilters[propertyName] = new CompiledFilter(lambda.Compile());
        else
            _dateAfterFilters[propertyName] = new CompiledFilter(lambda.Compile());
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

    private static DateTime? ToDateTime(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime;
        }
        if (value is string stringValue)
        {
            return DateTime.Parse(stringValue);
        }
        if (value is JsonElement jsonElement && jsonElement.TryGetDateTime(out var toDateTime))
        {
            return toDateTime;
        }
        throw new InvalidCastException("Could not convert value to DateTime");
    }
}

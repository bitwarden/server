using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.PrivilegedAccessManagement.Models.Rules;

namespace Bit.Core.PrivilegedAccessManagement.Services;

public sealed partial class AccessRuleValidator : IAccessRuleValidator
{
    private const int MaxCompositeDepth = 3;
    private const int MaxCompositeChildren = 10;

    private static readonly HashSet<string> AllowedDays =
        new(StringComparer.OrdinalIgnoreCase) { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [GeneratedRegex(@"^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex TimeOfDayRegex();

    public AccessRuleValidationResult Validate(string? ruleJson)
    {
        if (ruleJson is null)
        {
            return AccessRuleValidationResult.Valid;
        }

        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return AccessRuleValidationResult.Invalid("Rule JSON cannot be empty.");
        }

        Rule? rule;
        try
        {
            rule = JsonSerializer.Deserialize<Rule>(ruleJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return AccessRuleValidationResult.Invalid($"Rule JSON is malformed: {ex.Message}");
        }

        if (rule is null)
        {
            return AccessRuleValidationResult.Invalid("Rule must be an object.");
        }

        return ValidateRule(rule, depth: 0);
    }

    private static AccessRuleValidationResult ValidateRule(Rule rule, int depth)
    {
        return rule switch
        {
            HumanApprovalRule => AccessRuleValidationResult.Valid,
            IpAllowlistRule ip => ValidateIpAllowlist(ip),
            TimeOfDayRule tod => ValidateTimeOfDay(tod),
            AllOfRule all => ValidateAllOf(all, depth),
            _ => AccessRuleValidationResult.Invalid($"Unsupported rule kind: {rule.GetType().Name}."),
        };
    }

    private static AccessRuleValidationResult ValidateIpAllowlist(IpAllowlistRule rule)
    {
        if (rule.Cidrs.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("ip_allowlist requires at least one CIDR.");
        }

        foreach (var cidr in rule.Cidrs)
        {
            if (string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
            {
                return AccessRuleValidationResult.Invalid($"Invalid CIDR: '{cidr}'.");
            }
        }

        return AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateTimeOfDay(TimeOfDayRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Tz))
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires a tz.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(rule.Tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return AccessRuleValidationResult.Invalid($"Unknown timezone: '{rule.Tz}'.");
        }
        catch (InvalidTimeZoneException)
        {
            return AccessRuleValidationResult.Invalid($"Invalid timezone: '{rule.Tz}'.");
        }

        if (rule.Windows.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires at least one window.");
        }

        foreach (var window in rule.Windows)
        {
            if (window.Days.Count == 0)
            {
                return AccessRuleValidationResult.Invalid("time_of_day window requires at least one day.");
            }

            foreach (var day in window.Days)
            {
                if (!AllowedDays.Contains(day))
                {
                    return AccessRuleValidationResult.Invalid($"Invalid day: '{day}'.");
                }
            }

            if (!TimeOfDayRegex().IsMatch(window.From))
            {
                return AccessRuleValidationResult.Invalid($"Invalid 'from' time: '{window.From}'. Expected HH:mm.");
            }

            if (!TimeOfDayRegex().IsMatch(window.To))
            {
                return AccessRuleValidationResult.Invalid($"Invalid 'to' time: '{window.To}'. Expected HH:mm.");
            }
        }

        return AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateAllOf(AllOfRule rule, int depth)
    {
        if (depth >= MaxCompositeDepth)
        {
            return AccessRuleValidationResult.Invalid($"all_of nesting exceeds maximum depth of {MaxCompositeDepth}.");
        }

        if (rule.Rules.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("all_of requires at least one child rule.");
        }

        if (rule.Rules.Count > MaxCompositeChildren)
        {
            return AccessRuleValidationResult.Invalid($"all_of cannot contain more than {MaxCompositeChildren} child rules.");
        }

        foreach (var child in rule.Rules)
        {
            var childResult = ValidateRule(child, depth + 1);
            if (!childResult.IsValid)
            {
                return childResult;
            }
        }

        return AccessRuleValidationResult.Valid;
    }
}

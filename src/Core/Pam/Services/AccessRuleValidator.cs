using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.Pam.Models.Conditions;

namespace Bit.Core.Pam.Services;

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

    public AccessRuleValidationResult Validate(string? conditionsJson)
    {
        if (conditionsJson is null)
        {
            return AccessRuleValidationResult.Valid;
        }

        if (string.IsNullOrWhiteSpace(conditionsJson))
        {
            return AccessRuleValidationResult.Invalid("Conditions JSON cannot be empty.");
        }

        AccessCondition? condition;
        try
        {
            condition = JsonSerializer.Deserialize<AccessCondition>(conditionsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return AccessRuleValidationResult.Invalid($"Conditions JSON is malformed: {ex.Message}");
        }

        if (condition is null)
        {
            return AccessRuleValidationResult.Invalid("Conditions must be an object.");
        }

        return ValidateCondition(condition, depth: 0);
    }

    private static AccessRuleValidationResult ValidateCondition(AccessCondition condition, int depth)
    {
        return condition switch
        {
            HumanApprovalCondition => AccessRuleValidationResult.Valid,
            IpAllowlistCondition ip => ValidateIpAllowlist(ip),
            TimeOfDayCondition tod => ValidateTimeOfDay(tod),
            AllOfCondition all => ValidateAllOf(all, depth),
            _ => AccessRuleValidationResult.Invalid($"Unsupported condition kind: {condition.GetType().Name}."),
        };
    }

    private static AccessRuleValidationResult ValidateIpAllowlist(IpAllowlistCondition condition)
    {
        if (condition.Cidrs.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("ip_allowlist requires at least one CIDR.");
        }

        foreach (var cidr in condition.Cidrs)
        {
            if (string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
            {
                return AccessRuleValidationResult.Invalid($"Invalid CIDR: '{cidr}'.");
            }
        }

        return AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateTimeOfDay(TimeOfDayCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Tz))
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires a tz.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(condition.Tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return AccessRuleValidationResult.Invalid($"Unknown timezone: '{condition.Tz}'.");
        }
        catch (InvalidTimeZoneException)
        {
            return AccessRuleValidationResult.Invalid($"Invalid timezone: '{condition.Tz}'.");
        }

        if (condition.Windows.Count == 0)
        {
            return AccessRuleValidationResult.Invalid("time_of_day requires at least one window.");
        }

        foreach (var window in condition.Windows)
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

    private static AccessRuleValidationResult ValidateAllOf(AllOfCondition condition, int depth)
    {
        if (depth >= MaxCompositeDepth)
        {
            return AccessRuleValidationResult.Invalid($"all_of nesting exceeds maximum depth of {MaxCompositeDepth}.");
        }

        // An empty all_of is allowed: it is vacuously satisfied, so the rule governs its collections — routing
        // access through the PAM flow for audit logging — without imposing any gating condition. The engine
        // evaluates it to Allow.
        if (condition.Conditions.Count > MaxCompositeChildren)
        {
            return AccessRuleValidationResult.Invalid($"all_of cannot contain more than {MaxCompositeChildren} child conditions.");
        }

        foreach (var child in condition.Conditions)
        {
            var childResult = ValidateCondition(child, depth + 1);
            if (!childResult.IsValid)
            {
                return childResult;
            }
        }

        return AccessRuleValidationResult.Valid;
    }
}

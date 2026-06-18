using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Pam.Models.Conditions;

namespace Bit.Pam.Services;

public sealed partial class AccessRuleValidator : IAccessRuleValidator
{
    private const int MaxConditions = 10;

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

        List<AccessCondition>? conditions;
        try
        {
            conditions = JsonSerializer.Deserialize<List<AccessCondition>>(conditionsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return AccessRuleValidationResult.Invalid($"Conditions JSON is malformed: {ex.Message}");
        }

        if (conditions is null)
        {
            return AccessRuleValidationResult.Invalid("Conditions must be an array.");
        }

        // An empty list is allowed: it is vacuously satisfied, so the rule governs its collections — routing access
        // through the PAM flow for audit logging — without imposing any gating condition. The engine evaluates it
        // to Allow.
        if (conditions.Count > MaxConditions)
        {
            return AccessRuleValidationResult.Invalid($"Conditions cannot contain more than {MaxConditions} conditions.");
        }

        foreach (var condition in conditions)
        {
            var result = ValidateCondition(condition);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateCondition(AccessCondition? condition)
    {
        return condition switch
        {
            HumanApprovalCondition => AccessRuleValidationResult.Valid,
            IpAllowlistCondition ip => ValidateIpAllowlist(ip),
            TimeOfDayCondition tod => ValidateTimeOfDay(tod),
            null => AccessRuleValidationResult.Invalid("Conditions cannot contain a null entry."),
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

            // Day tokens are validated during deserialization by AccessWeekdayJsonConverter; an unknown token fails
            // the JSON parse above and is reported as malformed.
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
}

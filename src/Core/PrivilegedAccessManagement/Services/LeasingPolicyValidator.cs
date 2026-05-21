using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.PrivilegedAccessManagement.Models.Policies;

namespace Bit.Core.PrivilegedAccessManagement.Services;

public sealed partial class LeasingPolicyValidator : ILeasingPolicyValidator
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

    public LeasingPolicyValidationResult Validate(string? policyJson)
    {
        if (policyJson is null)
        {
            return LeasingPolicyValidationResult.Valid;
        }

        if (string.IsNullOrWhiteSpace(policyJson))
        {
            return LeasingPolicyValidationResult.Invalid("Policy JSON cannot be empty.");
        }

        LeasingPolicy? policy;
        try
        {
            policy = JsonSerializer.Deserialize<LeasingPolicy>(policyJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return LeasingPolicyValidationResult.Invalid($"Policy JSON is malformed: {ex.Message}");
        }

        if (policy is null)
        {
            return LeasingPolicyValidationResult.Invalid("Policy must be an object.");
        }

        return ValidatePolicy(policy, depth: 0);
    }

    private static LeasingPolicyValidationResult ValidatePolicy(LeasingPolicy policy, int depth)
    {
        return policy switch
        {
            HumanApprovalPolicy => LeasingPolicyValidationResult.Valid,
            IpAllowlistPolicy ip => ValidateIpAllowlist(ip),
            TimeOfDayPolicy tod => ValidateTimeOfDay(tod),
            AllOfPolicy all => ValidateAllOf(all, depth),
            _ => LeasingPolicyValidationResult.Invalid($"Unsupported policy kind: {policy.GetType().Name}."),
        };
    }

    private static LeasingPolicyValidationResult ValidateIpAllowlist(IpAllowlistPolicy policy)
    {
        if (policy.Cidrs.Count == 0)
        {
            return LeasingPolicyValidationResult.Invalid("ip_allowlist requires at least one CIDR.");
        }

        foreach (var cidr in policy.Cidrs)
        {
            if (string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
            {
                return LeasingPolicyValidationResult.Invalid($"Invalid CIDR: '{cidr}'.");
            }
        }

        return LeasingPolicyValidationResult.Valid;
    }

    private static LeasingPolicyValidationResult ValidateTimeOfDay(TimeOfDayPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Tz))
        {
            return LeasingPolicyValidationResult.Invalid("time_of_day requires a tz.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(policy.Tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return LeasingPolicyValidationResult.Invalid($"Unknown timezone: '{policy.Tz}'.");
        }
        catch (InvalidTimeZoneException)
        {
            return LeasingPolicyValidationResult.Invalid($"Invalid timezone: '{policy.Tz}'.");
        }

        if (policy.Windows.Count == 0)
        {
            return LeasingPolicyValidationResult.Invalid("time_of_day requires at least one window.");
        }

        foreach (var window in policy.Windows)
        {
            if (window.Days.Count == 0)
            {
                return LeasingPolicyValidationResult.Invalid("time_of_day window requires at least one day.");
            }

            foreach (var day in window.Days)
            {
                if (!AllowedDays.Contains(day))
                {
                    return LeasingPolicyValidationResult.Invalid($"Invalid day: '{day}'.");
                }
            }

            if (!TimeOfDayRegex().IsMatch(window.From))
            {
                return LeasingPolicyValidationResult.Invalid($"Invalid 'from' time: '{window.From}'. Expected HH:mm.");
            }

            if (!TimeOfDayRegex().IsMatch(window.To))
            {
                return LeasingPolicyValidationResult.Invalid($"Invalid 'to' time: '{window.To}'. Expected HH:mm.");
            }
        }

        return LeasingPolicyValidationResult.Valid;
    }

    private static LeasingPolicyValidationResult ValidateAllOf(AllOfPolicy policy, int depth)
    {
        if (depth >= MaxCompositeDepth)
        {
            return LeasingPolicyValidationResult.Invalid($"all_of nesting exceeds maximum depth of {MaxCompositeDepth}.");
        }

        if (policy.Policies.Count == 0)
        {
            return LeasingPolicyValidationResult.Invalid("all_of requires at least one child policy.");
        }

        if (policy.Policies.Count > MaxCompositeChildren)
        {
            return LeasingPolicyValidationResult.Invalid($"all_of cannot contain more than {MaxCompositeChildren} child policies.");
        }

        foreach (var child in policy.Policies)
        {
            var childResult = ValidatePolicy(child, depth + 1);
            if (!childResult.IsValid)
            {
                return childResult;
            }
        }

        return LeasingPolicyValidationResult.Valid;
    }
}

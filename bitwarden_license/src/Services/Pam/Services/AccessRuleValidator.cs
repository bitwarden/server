using System.Net;
using System.Text.Json;
using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Services;

public sealed class AccessRuleValidator : IAccessRuleValidator
{
    private const int MaxConditions = 10;

    // Stateless, so one shared instance serves every call.
    private static readonly ConditionValidator _conditionValidator = new();

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
            conditions = JsonSerializer.Deserialize<List<AccessCondition>>(conditionsJson, AccessConditionJson.Options);
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

        return conditions.Select(ValidateCondition).FirstOrDefault(result => !result.IsValid)
            ?? AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateCondition(AccessCondition? condition) =>
        condition is null
            ? AccessRuleValidationResult.Invalid("Conditions cannot contain a null entry.")
            : condition.Accept(_conditionValidator);

    /// <summary>
    /// Checks a single condition is well-formed. Stateless, mirroring the engine's evaluator: neither public
    /// service is itself a visitor; each delegates to a private one.
    /// </summary>
    private sealed class ConditionValidator : IAccessConditionVisitor<AccessRuleValidationResult>
    {
        public AccessRuleValidationResult VisitHumanApproval(HumanApprovalCondition condition) =>
            AccessRuleValidationResult.Valid;

        public AccessRuleValidationResult VisitIpAllowlist(IpAllowlistCondition condition)
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
    }
}

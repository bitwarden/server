using System.Text.Json;
using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Services;

public sealed class AccessRuleValidator : IAccessRuleValidator
{
    private const int MaxConditions = 10;

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

        // Each condition validates itself; the validator only enforces the document-level shape (it is an array,
        // within the size limit) and guards the one thing a condition cannot check for itself: a null entry.
        return conditions.Select(ValidateCondition).FirstOrDefault(result => !result.IsValid)
            ?? AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateCondition(AccessCondition? condition) =>
        // A JSON null in the array is not a condition and cannot validate itself, so it is rejected here.
        condition is null
            ? AccessRuleValidationResult.Invalid("Conditions cannot contain a null entry.")
            : condition.Validate();
}

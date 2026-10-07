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
        // An unmappable kind surfaces as NotSupportedException, not JsonException, and would otherwise be a 500. Its
        // message names internal types, so it is not relayed.
        catch (NotSupportedException)
        {
            return AccessRuleValidationResult.Invalid("Each condition must specify a valid 'kind'.");
        }

        if (conditions is null)
        {
            return AccessRuleValidationResult.Invalid("Conditions must be an array.");
        }

        // An empty list is allowed: the engine evaluates it to Allow, and the rule still routes access to its
        // collections through PAM for auditing.
        if (conditions.Count > MaxConditions)
        {
            return AccessRuleValidationResult.Invalid($"Conditions cannot contain more than {MaxConditions} conditions.");
        }

        return conditions.Select(ValidateCondition).FirstOrDefault(result => !result.IsValid)
            ?? AccessRuleValidationResult.Valid;
    }

    private static AccessRuleValidationResult ValidateCondition(AccessCondition? condition) =>
        // A JSON null entry is not a condition, so it cannot validate itself.
        condition is null
            ? AccessRuleValidationResult.Invalid("Conditions cannot contain a null entry.")
            : condition.Validate();
}

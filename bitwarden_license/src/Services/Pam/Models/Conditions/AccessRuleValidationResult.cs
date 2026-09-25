namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// The result of checking a condition (or a rule's whole conditions document) is well-formed: whether it is valid
/// and, when not, an actionable message. Produced at write time by <see cref="AccessCondition.Validate"/> and by
/// <see cref="Bit.Services.Pam.Services.IAccessRuleValidator"/>.
/// </summary>
public sealed record AccessRuleValidationResult(bool IsValid, string? Error)
{
    public static AccessRuleValidationResult Valid { get; } = new(true, null);
    public static AccessRuleValidationResult Invalid(string error) => new(false, error);
}

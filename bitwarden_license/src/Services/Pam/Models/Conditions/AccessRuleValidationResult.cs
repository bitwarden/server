namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// The write-time result of checking that a condition, or a rule's whole conditions document, is well-formed.
/// </summary>
public sealed record AccessRuleValidationResult(bool IsValid, string? Error)
{
    public static AccessRuleValidationResult Valid { get; } = new(true, null);
    public static AccessRuleValidationResult Invalid(string error) => new(false, error);
}

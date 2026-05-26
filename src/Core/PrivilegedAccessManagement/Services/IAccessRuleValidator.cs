namespace Bit.Core.PrivilegedAccessManagement.Services;

public interface IAccessRuleValidator
{
    /// <summary>
    /// Validates a raw JSON rule. A null or empty rule is treated as "no rule
    /// configured" and considered valid; callers decide how to treat that semantically.
    /// </summary>
    AccessRuleValidationResult Validate(string? ruleJson);
}

public sealed record AccessRuleValidationResult(bool IsValid, string? Error)
{
    public static AccessRuleValidationResult Valid { get; } = new(true, null);
    public static AccessRuleValidationResult Invalid(string error) => new(false, error);
}

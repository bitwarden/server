namespace Bit.Commercial.Pam.Services;

public interface IAccessRuleValidator
{
    /// <summary>
    /// Validates a raw JSON conditions document. A null or empty document is treated as "no conditions
    /// configured" and considered valid; callers decide how to treat that semantically.
    /// </summary>
    AccessRuleValidationResult Validate(string? conditionsJson);
}

public sealed record AccessRuleValidationResult(bool IsValid, string? Error)
{
    public static AccessRuleValidationResult Valid { get; } = new(true, null);
    public static AccessRuleValidationResult Invalid(string error) => new(false, error);
}

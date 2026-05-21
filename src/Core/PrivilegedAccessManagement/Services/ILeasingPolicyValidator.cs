namespace Bit.Core.PrivilegedAccessManagement.Services;

public interface ILeasingPolicyValidator
{
    /// <summary>
    /// Validates a raw JSON leasing policy. A null or empty policy is treated as "no policy
    /// configured" and considered valid; callers decide how to treat that semantically.
    /// </summary>
    LeasingPolicyValidationResult Validate(string? policyJson);
}

public sealed record LeasingPolicyValidationResult(bool IsValid, string? Error)
{
    public static LeasingPolicyValidationResult Valid { get; } = new(true, null);
    public static LeasingPolicyValidationResult Invalid(string error) => new(false, error);
}

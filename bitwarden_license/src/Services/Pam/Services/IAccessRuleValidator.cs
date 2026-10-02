using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Services;

public interface IAccessRuleValidator
{
    /// <summary>
    /// Validates a raw JSON conditions document. A null or empty document is treated as "no conditions
    /// configured" and considered valid; callers decide how to treat that semantically.
    /// </summary>
    AccessRuleValidationResult Validate(string? conditionsJson);
}

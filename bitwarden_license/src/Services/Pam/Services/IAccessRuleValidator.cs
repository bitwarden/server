using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Services;

public interface IAccessRuleValidator
{
    /// <summary>
    /// Validates a raw JSON conditions document. Null and an empty array both mean no conditions and are valid; a
    /// blank string is not.
    /// </summary>
    AccessRuleValidationResult Validate(string? conditionsJson);
}

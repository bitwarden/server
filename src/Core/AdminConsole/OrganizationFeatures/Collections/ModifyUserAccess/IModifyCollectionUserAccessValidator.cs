using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

/// <summary>
/// Checks whether an add/update/remove delta to collection user access may be applied.
/// </summary>
public interface IModifyCollectionUserAccessValidator
{
    Task<ValidationResult<ModifyCollectionUserAccessRequest>> ValidateAsync(ModifyCollectionUserAccessRequest request);
}

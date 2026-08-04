using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

/// <summary>
/// Checks whether an add/update/remove delta to collection group access may be applied.
/// </summary>
public interface IModifyCollectionGroupAccessValidator
{
    Task<ValidationResult<ModifyCollectionGroupAccessRequest>> ValidateAsync(ModifyCollectionGroupAccessRequest request);
}

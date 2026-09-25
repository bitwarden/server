using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections;

public interface ICollectionAccessValidator
{
    /// <summary>
    /// Checks that each group and each member belongs to the collection's organization.
    /// </summary>
    Task<ValidationResult<CollectionAccessValidationRequest>> ValidateAsync(
        CollectionAccessValidationRequest request);
}

using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

/// <summary>
/// Checks whether an add/update/remove delta is allowed.
/// </summary>
/// <remarks>
/// Catches duplicate/conflicting ids, blocks changes to a DefaultUserCollection, stops a user from
/// adding themselves, and makes sure every target still has a manager.
/// </remarks>
public interface IModifyCollectionUserAccessValidator
{
    /// <summary>
    /// Validates the delta.
    /// </summary>
    /// <param name="request">The targets and the delta to validate.</param>
    /// <returns>A <see cref="ValidationResult{TRequest}"/> that is valid if the delta may be applied.</returns>
    Task<ValidationResult<ModifyCollectionUserAccessRequest>> ValidateAsync(ModifyCollectionUserAccessRequest request);
}

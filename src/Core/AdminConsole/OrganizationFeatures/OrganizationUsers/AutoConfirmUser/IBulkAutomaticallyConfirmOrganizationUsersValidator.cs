using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

/// <summary>
/// Validates a batch of <see cref="AutomaticallyConfirmOrganizationUserValidationRequest"/> objects using
/// bulk data fetches so that each data dependency is retrieved once for the entire batch rather than once per user.
/// </summary>
public interface IBulkAutomaticallyConfirmOrganizationUsersValidator
{
    /// <summary>
    /// Validates all <paramref name="requests"/> in a single bulk pass.
    /// </summary>
    /// <remarks>
    /// The caller must already have populated <see cref="AutomaticallyConfirmOrganizationUserValidationRequest.OrganizationUser"/>
    /// and <see cref="AutomaticallyConfirmOrganizationUserValidationRequest.Organization"/> on each request.
    /// All requests must belong to the same organization.
    /// </remarks>
    /// <param name="requests">The hydrated validation requests to validate.</param>
    /// <returns>One <see cref="ValidationResult{T}"/> per input request, preserving order.</returns>
    Task<IEnumerable<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>> ValidateManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests);
}

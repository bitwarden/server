using Bit.Core.AdminConsole.Entities;
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
    /// <param name="requests">The hydrated per-user validation requests to validate.</param>
    /// <param name="organization">The organization shared by all requests.</param>
    /// <returns>One <see cref="ValidationResult{T}"/> per input request.</returns>
    Task<IEnumerable<ValidationResult<AutomaticallyConfirmOrganizationUserValidationRequest>>> ValidateManyAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserValidationRequest> requests,
        Organization organization);
}

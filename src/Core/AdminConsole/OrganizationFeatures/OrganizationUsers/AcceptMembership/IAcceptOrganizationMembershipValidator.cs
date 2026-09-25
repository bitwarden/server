using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

/// <summary>
/// Validates that a user is eligible to join an organization when accepting a membership invitation.
/// </summary>
/// <remarks>
/// The following policies are enforced:
/// <list type="bullet">
///     <item>Automatic User Confirmation policy (includes the gated provider and multi-org checks)</item>
///     <item>Single Organization policy</item>
///     <item>Two-Factor Authentication policy</item>
/// </list>
/// </remarks>
public interface IAcceptOrganizationMembershipValidator
{
    /// <summary>
    /// Validates that the user is eligible to join the organization.
    /// </summary>
    /// <param name="request">The request containing the user, organization, and existing membership data.</param>
    /// <returns>
    /// A <see cref="ValidationResult{TRequest}"/> that is valid if the user may join, with
    /// <see cref="AcceptOrganizationMembershipValidationResult.AutoConfirmPolicyEnabled"/> indicating
    /// whether the Automatic User Confirmation policy is enabled for the joining organization.
    /// </returns>
    Task<ValidationResult<AcceptOrganizationMembershipValidationResult>> ValidateAsync(
        AcceptOrganizationMembershipValidationRequest request);
}

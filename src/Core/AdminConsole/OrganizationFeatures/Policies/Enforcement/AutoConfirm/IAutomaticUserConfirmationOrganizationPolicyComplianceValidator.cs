using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Validates that an organization meets the prerequisites for enabling the Automatic User Confirmation policy.
/// </summary>
/// <remarks>
/// The following conditions must be met:
/// <list type="bullet">
///     <item>All non-invited organization users belong only to this organization (Single Organization compliance)</item>
///     <item>No organization users are provider members</item>
/// </list>
/// </remarks>
public interface IAutomaticUserConfirmationOrganizationPolicyComplianceValidator
{
    /// <summary>
    /// Checks whether the organization is compliant with the Automatic User Confirmation policy prerequisites.
    /// </summary>
    /// <param name="request">The request containing the organization ID to validate.</param>
    /// <returns>
    /// A <see cref="ValidationResult{TRequest}"/> that is valid if the organization is compliant,
    /// or contains a <see cref="UserNotCompliantWithSingleOrganization"/> or <see cref="ProviderExistsInOrganization"/>
    /// error if validation fails.
    /// </returns>
    Task<ValidationResult<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>>
        IsOrganizationCompliantAsync(AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest request);
}

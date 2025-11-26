using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

/// <summary>
/// Used to enforce the Automatic User Confirmation policy. It uses the <see cref="IPolicyRequirementQuery"/> to retrieve
/// the <see cref="AutomaticUserConfirmationPolicyRequirement"/>. It is used to check to make sure the given user is
/// valid for the Automatic User Confirmation policy. It also validates that the given user is not a provider
/// or a member of another organization regardless of status or type.
/// </summary>
public interface IAutomaticUserConfirmationPolicyEnforcementQuery
{

    /// <summary>
    /// Checks if the given user is compliant with the Automatic User Confirmation policy. To be compliant, the user must:
    ///
    ///
    /// </summary>
    /// <param name="request"></param>
    /// <remarks>
    /// This uses the validation result pattern to avoid throwing exceptions.
    /// </remarks>
    /// <returns>A validation result with the error message if applicable.</returns>
    Task<ValidationResult<AutomaticUserConfirmationPolicyEnforcementRequest>> IsCompliantAsync(AutomaticUserConfirmationPolicyEnforcementRequest request);
}

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Represents an event handler for the Automatic User Confirmation policy.
///
/// This class validates that the following conditions are met:
/// <ul>
///     <li>The Single organization policy is enabled</li>
///     <li>All organization users are compliant with the Single organization policy</li>
///     <li>No provider users exist</li>
/// </ul>
/// </summary>
public class AutomaticUserConfirmationPolicyEventHandler(IAutomaticUserConfirmationOrganizationPolicyComplianceValidator validator)
    : IPolicyValidator, IPolicyValidationEvent, IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.AutomaticUserConfirmation;

    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public async Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        var isNotEnablingPolicy = policyUpdate is not { Enabled: true };
        var policyAlreadyEnabled = currentPolicy is { Enabled: true };
        if (isNotEnablingPolicy || policyAlreadyEnabled)
        {
            return string.Empty;
        }

        return (await validator.IsOrganizationCompliantAsync(
            new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId)))
            .Match(
                error => error.Message,
                _ => string.Empty);
    }

    public async Task<string> ValidateAsync(SavePolicyModel savePolicyModel, Policy? currentPolicy) =>
        await ValidateAsync(savePolicyModel.PolicyUpdate, currentPolicy);

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) =>
        Task.CompletedTask;
}

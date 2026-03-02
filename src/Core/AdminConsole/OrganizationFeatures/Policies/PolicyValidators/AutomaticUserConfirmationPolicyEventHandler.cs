using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Repositories;

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
public class AutomaticUserConfirmationPolicyEventHandler(
    IAutomaticUserConfirmationOrganizationPolicyComplianceValidator validator,
    IOrganizationUserRepository organizationUserRepository,
    IDeleteEmergencyAccessCommand deleteEmergencyAccessCommand)
    : IPolicyValidator, IPolicyValidationEvent, IEnforceDependentPoliciesEvent, IOnPolicyPreUpdateEvent
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

    public async Task ExecutePreUpsertSideEffectAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        var isNotEnablingPolicy = policyRequest.PolicyUpdate is not { Enabled: true };
        var policyAlreadyEnabled = currentPolicy is { Enabled: true };
        if (isNotEnablingPolicy || policyAlreadyEnabled)
        {
            return;
        }

        var orgUsers = await organizationUserRepository.GetManyByOrganizationAsync(policyRequest.PolicyUpdate.OrganizationId, null);
        var orgUserIds = orgUsers.Where(w => w.UserId != null).Select(s => s.UserId!.Value).ToList();

        await deleteEmergencyAccessCommand.DeleteAllByUserIdsAsync(orgUserIds);
    }

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.CompletedTask;
}

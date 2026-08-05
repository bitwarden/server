using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class FillAssistPolicyEventHandler : IPolicyValidationEvent
{
    public PolicyType Type => PolicyType.FillAssist;

    public Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy) =>
        policyRequest.PolicyUpdate is { Enabled: true } && string.IsNullOrEmpty(policyRequest.PolicyUpdate.Data)
            ? Task.FromResult("The RulesUrl field is required to enable the Fill Assist policy.")
            : Task.FromResult(string.Empty);
}

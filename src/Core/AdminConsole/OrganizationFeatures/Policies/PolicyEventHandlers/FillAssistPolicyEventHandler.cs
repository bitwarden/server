using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

public class FillAssistPolicyEventHandler : IPolicyValidationEvent, IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.FillAssist;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];

    public Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        if (policyRequest.PolicyUpdate is not { Enabled: true })
        {
            return Task.FromResult(string.Empty);
        }

        var data = CoreHelpers.LoadClassFromJsonData<FillAssistPolicyData>(policyRequest.PolicyUpdate.Data);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(data, new ValidationContext(data), results, validateAllProperties: true);

        return Task.FromResult(
            isValid
                ? string.Empty
                : string.Join(", ", results.Select(r => r.ErrorMessage)));
    }
}

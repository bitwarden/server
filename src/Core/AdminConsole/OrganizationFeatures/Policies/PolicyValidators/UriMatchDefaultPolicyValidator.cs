﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class UriMatchDefaultPolicyValidator : IPolicyValidator, IEnforceDependentPoliciesEvent
{
    public PolicyType Type => PolicyType.UriMatchDefaults;
    public IEnumerable<PolicyType> RequiredPolicies => [PolicyType.SingleOrg];
    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");
    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.CompletedTask;
}

#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

public interface IVNextSavePolicyCommand
{
    Task<Policy> SaveAsync(SavePolicyModel policyRequest);
}

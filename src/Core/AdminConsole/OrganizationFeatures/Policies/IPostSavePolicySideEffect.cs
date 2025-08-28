using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPostSavePolicySideEffect
{
    public Task ExecuteSideEffectsAsync(SavePolicyModel policyUpdate, Policy? currentPolicy);
}

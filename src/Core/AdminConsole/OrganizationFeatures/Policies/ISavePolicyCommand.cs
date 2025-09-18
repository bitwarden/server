using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface ISavePolicyCommand
{
    Task<Policy> SaveAsync(PolicyUpdate policy);

    /// <summary>
    /// FIXME: this is a first pass at implementing side effects after the policy has been saved, which was not supported by the validator pattern.
    /// However, this needs to be implemented in a policy-agnostic way rather than building out switch statements in the command itself.
    /// </summary>
    Task<Policy> VNextSaveAsync(SavePolicyModel policyRequest);
}

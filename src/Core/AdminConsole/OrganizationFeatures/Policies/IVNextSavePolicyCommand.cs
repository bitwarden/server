using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

#nullable enable
namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IVNextSavePolicyCommand
{
    Task<Policy> SaveAsync(SavePolicyModel policyRequest);
}

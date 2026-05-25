using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface IUpdateLeasingPolicyCommand
{
    Task<LeasingPolicy> UpdateAsync(Guid organizationId, Guid id, LeasingPolicy update);
}

using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface ICreateLeasingPolicyCommand
{
    Task<LeasingPolicy> CreateAsync(LeasingPolicy policy);
}

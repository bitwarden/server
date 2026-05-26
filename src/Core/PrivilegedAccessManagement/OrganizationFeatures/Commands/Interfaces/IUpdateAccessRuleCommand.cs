using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface IUpdateAccessRuleCommand
{
    Task<AccessRule> UpdateAsync(Guid organizationId, Guid id, AccessRule update);
}

using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface ICreateAccessRuleCommand
{
    Task<AccessRule> CreateAsync(AccessRule rule);
}

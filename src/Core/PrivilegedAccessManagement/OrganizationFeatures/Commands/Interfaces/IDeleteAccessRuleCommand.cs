namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface IDeleteAccessRuleCommand
{
    Task DeleteAsync(Guid organizationId, Guid id);
}

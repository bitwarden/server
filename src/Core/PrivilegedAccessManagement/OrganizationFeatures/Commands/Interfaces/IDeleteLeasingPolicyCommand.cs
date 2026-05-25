namespace Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands.Interfaces;

public interface IDeleteLeasingPolicyCommand
{
    Task DeleteAsync(Guid organizationId, Guid id);
}

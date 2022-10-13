namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteGroupCommand
{
    Task DeleteGroupAsync(Guid organizationId, Guid id);
}

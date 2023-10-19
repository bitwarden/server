using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Interfaces;

public interface IDeleteGroupCommand
{
    Task DeleteGroupAsync(Guid organizationId, Guid id);
    Task DeleteGroupAsync(Guid organizationId, Guid id, EventSystemUser eventSystemUser);
    Task DeleteAsync(Group group);
    Task DeleteManyAsync(ICollection<Group> groups);
}

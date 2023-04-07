using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.Groups.Interfaces;

public interface IDeleteGroupCommand
{
    Task DeleteGroupAsync(Guid organizationId, Guid id);
    Task DeleteGroupAsync(Guid organizationId, Guid id, EventSystemUser eventSystemUser);
    Task DeleteAsync(Group group);
    Task DeleteManyAsync(ICollection<Group> groups);
}

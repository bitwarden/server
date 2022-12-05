using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.Groups.Interfaces;

public interface ICreateGroupCommand
{
    Task CreateGroupAsync(Group group, Organization organization,
        IEnumerable<SelectionReadOnly> collections = null);

    Task CreateGroupAsync(Group group, Organization organization, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null);

    void Validate(Organization organization);
}

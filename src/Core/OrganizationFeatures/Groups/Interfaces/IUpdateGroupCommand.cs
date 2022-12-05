using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.Groups.Interfaces;

public interface IUpdateGroupCommand
{
    Task UpdateGroupAsync(Group group,
        IEnumerable<SelectionReadOnly> collections = null);

    Task UpdateGroupAsync(Group group, EventSystemUser systemUser,
        IEnumerable<SelectionReadOnly> collections = null);

    void Validate(Organization organization);
}

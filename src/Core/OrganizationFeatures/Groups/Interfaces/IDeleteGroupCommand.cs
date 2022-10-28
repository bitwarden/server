using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.Groups.Interfaces;

public interface IDeleteGroupCommand
{
    Task DeleteGroupAsync(Guid organizationId, Guid id);
    Task DeleteGroupAsync(Guid organizationId, Guid id, EventSystemUser eventSystemUser);
}

using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces;

public interface IGetGroupCommand
{
    Task<ScimGroupResponseModel> GetGroupAsync(Guid organizationId, Guid groupId);
}

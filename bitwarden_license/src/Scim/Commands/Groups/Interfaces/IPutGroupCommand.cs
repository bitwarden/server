using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces;

public interface IPutGroupCommand
{
    Task<ScimGroupResponseModel> PutGroupAsync(Guid organizationId, Guid id, ScimGroupRequestModel model);
}

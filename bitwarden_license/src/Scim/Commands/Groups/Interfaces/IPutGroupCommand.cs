using Bit.Core.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces;

public interface IPutGroupCommand
{
    Task<Group> PutGroupAsync(Guid organizationId, Guid id, ScimGroupRequestModel model);
}

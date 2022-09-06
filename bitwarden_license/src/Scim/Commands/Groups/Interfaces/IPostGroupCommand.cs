using Bit.Core.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces;

public interface IPostGroupCommand
{
    Task<Group> PostGroupAsync(Guid organizationId, ScimGroupRequestModel model);
}

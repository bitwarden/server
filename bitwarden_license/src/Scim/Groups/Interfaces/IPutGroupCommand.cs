using Bit.Core.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IPutGroupCommand
{
    Task<Group> PutGroupAsync(Organization organization, Guid id, ScimGroupRequestModel model);
}

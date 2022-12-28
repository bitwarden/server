using Bit.Core.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IPatchGroupCommand
{
    Task PatchGroupAsync(Organization organization, Guid id, ScimPatchModel model);
}

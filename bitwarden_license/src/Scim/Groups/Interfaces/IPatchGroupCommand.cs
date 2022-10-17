using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IPatchGroupCommand
{
    Task PatchGroupAsync(Guid organizationId, Guid id, ScimPatchModel model);
}

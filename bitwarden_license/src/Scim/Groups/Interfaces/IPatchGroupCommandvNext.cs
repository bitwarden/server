using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IPatchGroupCommandvNext
{
    Task PatchGroupAsync(Guid organizationId, Guid groupId, ScimPatchModel model);
}

using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces
{
    public interface IPatchGroupCommand
    {
        Task PatchGroupAsync(Guid organizationId, Guid id, ScimPatchModel model);
    }
}

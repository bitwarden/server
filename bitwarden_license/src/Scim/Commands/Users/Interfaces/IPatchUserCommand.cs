using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users.Interfaces
{
    public interface IPatchUserCommand
    {
        Task PatchUserAsync(Guid organizationId, Guid id, ScimPatchModel model);
    }
}

using Bit.Scim.Models;

namespace Bit.Scim.Users.Interfaces;

public interface IPatchUserCommand
{
    Task PatchUserAsync(Guid organizationId, Guid id, ScimPatchModel model);
}

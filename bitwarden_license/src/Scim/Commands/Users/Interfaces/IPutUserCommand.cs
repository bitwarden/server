using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users.Interfaces;

public interface IPutUserCommand
{
    Task<ScimUserResponseModel> PutUserAsync(Guid organizationId, Guid id, ScimUserRequestModel model);
}

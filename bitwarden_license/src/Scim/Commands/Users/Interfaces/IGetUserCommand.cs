using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users.Interfaces
{
    public interface IGetUserCommand
    {
        Task<ScimUserResponseModel> GetUserAsync(Guid organizationId, Guid id);
    }
}

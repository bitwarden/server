using System;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users.Interfaces
{
    public interface IGetUsersListCommand
    {
        Task<ScimListResponseModel<ScimUserResponseModel>> GetUsersListAsync(Guid organizationId, string filter, int? count, int? startIndex);
    }
}

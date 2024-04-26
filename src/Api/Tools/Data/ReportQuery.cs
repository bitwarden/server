using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;

namespace Bit.Api.Tools.Data;

public class ReportQuery
{
    public async Task<ICollection<string>> GetUsers(Guid id, [Service] IOrganizationUserRepository userRepository)
    {
        var users = await userRepository.GetManyDetailsByOrganizationAsync(id, true, true);

        return users.Select(user => user.Email).ToList();
    }
}


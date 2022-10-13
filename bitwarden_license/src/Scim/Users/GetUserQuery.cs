using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Users;

public class GetUserQuery : IGetUserQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetUserQuery(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<ScimUserResponseModel> GetUserAsync(Guid organizationId, Guid id)
    {
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        return new ScimUserResponseModel(orgUser);
    }
}

using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Commands.Users.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users;

public class GetUserCommand : IGetUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetUserCommand(IOrganizationUserRepository organizationUserRepository)
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

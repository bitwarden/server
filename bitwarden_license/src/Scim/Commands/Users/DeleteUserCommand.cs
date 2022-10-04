using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users.Interfaces;

namespace Bit.Scim.Commands.Users;

public class DeleteUserCommand : IDeleteUserCommand
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public DeleteUserCommand(
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid id)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        await _organizationService.DeleteUserAsync(organizationId, id, null);
    }
}

using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class DeleteOrganizationUserCommand : IDeleteOrganizationUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public DeleteOrganizationUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        await _organizationService.DeleteUserAsync(organizationId, organizationUserId, deletingUserId);
    }

    public async Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        await _organizationService.DeleteUserAsync(organizationId, organizationUserId, eventSystemUser);
    }
}

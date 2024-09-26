using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class RemoveOrganizationUserCommand : IRemoveOrganizationUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public RemoveOrganizationUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService
    )
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId)
    {
        await ValidateDeleteUserAsync(organizationId, organizationUserId);

        await _organizationService.RemoveUserAsync(organizationId, organizationUserId, deletingUserId);
    }

    public async Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser)
    {
        await ValidateDeleteUserAsync(organizationId, organizationUserId);

        await _organizationService.RemoveUserAsync(organizationId, organizationUserId, eventSystemUser);
    }

    private async Task ValidateDeleteUserAsync(Guid organizationId, Guid organizationUserId)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(organizationUserId);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }
    }
}

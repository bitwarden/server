using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users;

public class PutUserCommand : IPutUserCommand
{
    private readonly IUserService _userService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public PutUserCommand(
        IUserService userService,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService)
    {
        _userService = userService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }

    public async Task<ScimUserResponseModel> PutUserAsync(Guid organizationId, Guid id, ScimUserRequestModel model)
    {
        var orgUser = await _organizationUserRepository.GetByIdAsync(id);
        if (orgUser == null || orgUser.OrganizationId != organizationId)
        {
            throw new NotFoundException("User not found.");
        }

        if (model.Active && orgUser.Status == OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RestoreUserAsync(orgUser, null, _userService);
        }
        else if (!model.Active && orgUser.Status != OrganizationUserStatusType.Revoked)
        {
            await _organizationService.RevokeUserAsync(orgUser, null);
        }

        // Have to get full details object for response model
        var orgUserDetails = await _organizationUserRepository.GetDetailsByIdAsync(id);
        return new ScimUserResponseModel(orgUserDetails);
    }
}

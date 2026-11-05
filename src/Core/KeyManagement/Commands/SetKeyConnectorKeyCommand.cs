using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Authorization;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.KeyManagement.Commands;

public class SetKeyConnectorKeyCommand : ISetKeyConnectorKeyCommand
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IEventService _eventService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public SetKeyConnectorKeyCommand(
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IEventService eventService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IUserService userService,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _eventService = eventService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _userService = userService;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    /// <inheritdoc />
    public async Task SetKeyConnectorKeyForUserAsync(User user, KeyConnectorKeysData keyConnectorKeysData,
        bool acceptOrganizationInvite = true)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User, user,
            KeyConnectorOperations.Use);
        if (!authorizationResult.Succeeded)
        {
            throw new BadRequestException("Cannot use Key Connector");
        }

        var orgUser = await GetOrganizationUserAsync(keyConnectorKeysData.OrgIdentifier, user);

        var updateUserDataTasks = new List<UpdateUserData>
        {
            _userRepository.SetKeyConnectorUserKey(user.Id, keyConnectorKeysData.KeyConnectorKeyWrappedUserKey)
        };

        if (keyConnectorKeysData.ContainedKeyId is not null)
        {
            updateUserDataTasks.Add(_userRepository.SetUserKeyId(user.Id, keyConnectorKeysData.ContainedKeyId));
        }

        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id,
            keyConnectorKeysData.AccountKeys.ToAccountKeysData(), updateUserDataTasks);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        if (acceptOrganizationInvite)
        {
            await _acceptOrgUserCommand.AcceptOrgUserAsync(orgUser, user, _userService);
        }
    }

    /// <summary>
    /// Retrieves the organization user for the specified organization SSO identifier and user.
    /// Throws a BadRequestException if the organization or user is not found.
    /// </summary>
    /// <param name="orgSsoIdentifier">The SSO identifier of the organization.</param>
    /// <param name="user">The user for whom to retrieve the organization user.</param>
    /// <returns>The organization user corresponding to the specified organization and user.</returns>
    /// <exception cref="BadRequestException"></exception>
    private async Task<OrganizationUser> GetOrganizationUserAsync(string orgSsoIdentifier, User user)
    {
        var org = await _organizationRepository.GetByIdentifierAsync(orgSsoIdentifier)
            ?? throw new BadRequestException(new OrganizationNotFoundError().Message);

        return await _organizationUserRepository.GetByOrganizationAsync(org.Id, user.Id)
            ?? throw new BadRequestException(new UserNotFoundInOrganizationError().Message);
    }
}

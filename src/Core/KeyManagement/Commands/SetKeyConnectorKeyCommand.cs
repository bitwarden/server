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

    public SetKeyConnectorKeyCommand(
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IEventService eventService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IUserService userService,
        IUserRepository userRepository)
    {
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _eventService = eventService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _userService = userService;
        _userRepository = userRepository;
    }

    public async Task SetKeyConnectorKeyForUserAsync(User user, KeyConnectorKeysData keyConnectorKeysData)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User, user,
            KeyConnectorOperations.Use);
        if (!authorizationResult.Succeeded)
        {
            throw new BadRequestException("Cannot use Key Connector");
        }

        var setKeyConnectorUserKeyTask =
            _userRepository.SetKeyConnectorUserKey(user.Id, keyConnectorKeysData.KeyConnectorKeyWrappedUserKey);

        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id,
            keyConnectorKeysData.AccountKeys.ToAccountKeysData(), [setKeyConnectorUserKeyTask]);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await _acceptOrgUserCommand.AcceptOrgUserByOrgSsoIdAsync(keyConnectorKeysData.OrgIdentifier, user,
            _userService);
    }
}

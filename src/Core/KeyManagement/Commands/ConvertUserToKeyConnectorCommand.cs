using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Authorization;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.KeyManagement.Commands;

public class ConvertUserToKeyConnectorCommand : IConvertUserToKeyConnectorCommand
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly IMasterPasswordService _masterPasswordService;

    public ConvertUserToKeyConnectorCommand(
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IUserRepository userRepository,
        IEventService eventService,
        IMasterPasswordService masterPasswordService)
    {
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _userRepository = userRepository;
        _eventService = eventService;
        _masterPasswordService = masterPasswordService;
    }

    public async Task ConvertAsync(User user, string? keyConnectorKeyWrappedUserKey = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var authorizationResult = await _authorizationService.AuthorizeAsync(_currentContext.HttpContext.User, user,
            KeyConnectorOperations.Use);
        if (!authorizationResult.Succeeded)
        {
            throw new BadRequestException("Cannot use Key Connector");
        }

        _masterPasswordService.PrepareClearMasterPassword(user);
        user.UsesKeyConnector = true;

        if (!string.IsNullOrWhiteSpace(keyConnectorKeyWrappedUserKey))
        {
            user.Key = keyConnectorKeyWrappedUserKey;
        }

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);
    }
}

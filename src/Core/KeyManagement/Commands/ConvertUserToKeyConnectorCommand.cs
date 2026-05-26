using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.KeyManagement.Commands;

public class ConvertUserToKeyConnectorCommand : IConvertUserToKeyConnectorCommand
{
    private readonly IUserRepository _userRepository;
    private readonly IEventService _eventService;
    private readonly ICurrentContext _currentContext;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IdentityErrorDescriber _identityErrorDescriber;
    private readonly ILogger<ConvertUserToKeyConnectorCommand> _logger;

    public ConvertUserToKeyConnectorCommand(
        IUserRepository userRepository,
        IEventService eventService,
        ICurrentContext currentContext,
        IMasterPasswordService masterPasswordService,
        IdentityErrorDescriber identityErrorDescriber,
        ILogger<ConvertUserToKeyConnectorCommand> logger)
    {
        _userRepository = userRepository;
        _eventService = eventService;
        _currentContext = currentContext;
        _masterPasswordService = masterPasswordService;
        _identityErrorDescriber = identityErrorDescriber;
        _logger = logger;
    }

    public async Task<IdentityResult> ConvertAsync(User user, string? keyConnectorKeyWrappedUserKey = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var validationFailure = ValidateCanUseKeyConnector(user);
        if (validationFailure != null)
        {
            return validationFailure;
        }

        _masterPasswordService.PrepareClearMasterPassword(user);
        user.UsesKeyConnector = true;

        if (!string.IsNullOrWhiteSpace(keyConnectorKeyWrappedUserKey))
        {
            user.Key = keyConnectorKeyWrappedUserKey;
        }

        await _userRepository.ReplaceAsync(user);
        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        return IdentityResult.Success;
    }

    private IdentityResult? ValidateCanUseKeyConnector(User user)
    {
        if (user.UsesKeyConnector)
        {
            _logger.LogWarning("Already uses Key Connector.");
            return IdentityResult.Failed(_identityErrorDescriber.UserAlreadyHasPassword());
        }

        if (_currentContext.Organizations.Any(u =>
                u.Type is OrganizationUserType.Owner or OrganizationUserType.Admin))
        {
            throw new BadRequestException("Cannot use Key Connector when admin or owner of an organization.");
        }

        return null;
    }
}

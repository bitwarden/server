using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.KeyManagement.Commands;

public class SetKeyConnectorKeyCommand : ISetKeyConnectorKeyCommand
{
    private readonly ICanUseKeyConnectorQuery _canUseKeyConnectorQuery;
    private readonly IEventService _eventService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;

    public SetKeyConnectorKeyCommand(
        ICanUseKeyConnectorQuery canUseKeyConnectorQuery,
        IEventService eventService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IUserService userService,
        IUserRepository userRepository)
    {
        _canUseKeyConnectorQuery = canUseKeyConnectorQuery;
        _eventService = eventService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _userService = userService;
        _userRepository = userRepository;
    }

    public async Task SetKeyConnectorKeyForUserAsync(User user, SetKeyConnectorKeyRequestModel requestModel)
    {
        // TODO remove validation with https://bitwarden.atlassian.net/browse/PM-27280
        if (string.IsNullOrEmpty(requestModel.KeyConnectorKeyWrappedUserKey) || requestModel.AccountKeys == null)
        {
            throw new BadRequestException("KeyConnectorKeyWrappedUserKey and AccountKeys must be provided");
        }

        _canUseKeyConnectorQuery.VerifyCanUseKeyConnector(user);

        var setKeyConnectorUserKeyTask =
            _userRepository.SetKeyConnectorUserKey(user.Id, requestModel.KeyConnectorKeyWrappedUserKey);

        await _userRepository.SetV2AccountCryptographicStateAsync(user.Id, requestModel.AccountKeys.ToAccountKeysData(),
            [setKeyConnectorUserKeyTask]);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await _acceptOrgUserCommand.AcceptOrgUserByOrgSsoIdAsync(requestModel.OrgIdentifier, user, _userService);
    }
}

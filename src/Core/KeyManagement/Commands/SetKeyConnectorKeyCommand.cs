using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands.Interfaces;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Services;

namespace Bit.Core.KeyManagement.Commands;

public class SetKeyConnectorKeyCommand : ISetKeyConnectorKeyCommand
{
    private readonly ICanUseKeyConnectorQuery _canUseKeyConnectorQuery;
    private readonly IEventService _eventService;
    private readonly IAcceptOrgUserCommand _acceptOrgUserCommand;
    private readonly IUserService _userService;
    private readonly ISetAccountKeysForUserCommand _setAccountKeysForUserCommand;

    public SetKeyConnectorKeyCommand(
        ICanUseKeyConnectorQuery canUseKeyConnectorQuery,
        IEventService eventService,
        IAcceptOrgUserCommand acceptOrgUserCommand,
        IUserService userService,
        ISetAccountKeysForUserCommand setAccountKeysForUserCommand)
    {
        _canUseKeyConnectorQuery = canUseKeyConnectorQuery;
        _eventService = eventService;
        _acceptOrgUserCommand = acceptOrgUserCommand;
        _userService = userService;
        _setAccountKeysForUserCommand = setAccountKeysForUserCommand;
    }

    public async Task SetKeyConnectorKeyForUserAsync(User user, SetKeyConnectorKeyRequestModel requestModel)
    {
        // TODO remove validation with https://bitwarden.atlassian.net/browse/PM-27280
        if (string.IsNullOrEmpty(requestModel.KeyConnectorKeyWrappedUserKey) || requestModel.AccountKeys == null)
        {
            throw new BadRequestException("KeyConnectorKeyWrappedUserKey and AccountKeys must be provided");
        }

        _canUseKeyConnectorQuery.VerifyCanUseKeyConnector(user);

        // Key Connector does not use KDF, so we set some defaults
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = AuthConstants.ARGON2_ITERATIONS.Default;
        user.KdfMemory = AuthConstants.ARGON2_MEMORY.Default;
        user.KdfParallelism = AuthConstants.ARGON2_PARALLELISM.Default;

        user.Key = requestModel.KeyConnectorKeyWrappedUserKey;

        user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
        user.UsesKeyConnector = true;

        await _setAccountKeysForUserCommand.SetAccountKeysForUserAsync(user, requestModel.AccountKeys);

        await _eventService.LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await _acceptOrgUserCommand.AcceptOrgUserByOrgSsoIdAsync(requestModel.OrgIdentifier, user, _userService);
    }
}

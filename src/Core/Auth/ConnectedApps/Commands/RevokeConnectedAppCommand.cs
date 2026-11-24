#nullable enable

using Bit.Core.Auth.ConnectedApps.Commands.Interfaces;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.ConnectedApps.Commands;

public class RevokeConnectedAppCommand : IRevokeConnectedAppCommand
{
    // Duende's persisted-grant type for a remembered user-consent grant. Referenced by value because
    // Core does not depend on Duende.IdentityServer.
    private const string UserConsentGrantType = "user_consent";

    private readonly IConnectedAppRepository _connectedAppRepository;
    private readonly IGrantRepository _grantRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public RevokeConnectedAppCommand(
        IConnectedAppRepository connectedAppRepository,
        IGrantRepository grantRepository,
        IPushNotificationService pushNotificationService)
    {
        _connectedAppRepository = connectedAppRepository;
        _grantRepository = grantRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task RevokeAsync(Guid userId, Guid connectionId)
    {
        var connection = await _connectedAppRepository.GetByIdAsync(connectionId);
        if (connection == null || connection.UserId != userId)
        {
            throw new NotFoundException();
        }

        await _connectedAppRepository.DeleteAsync(connection);

        // Also drop the remembered OIDC consent grant for this partner, so a later connect goes back
        // through the consent screen (minting a fresh PSK) instead of silently returning a code that
        // has no connection behind it.
        // A null session id matches every session for this subject + client + type (the proc treats
        // null params as wildcards).
        await _grantRepository.DeleteManyAsync(
            userId.ToString(), null!, connection.PartnerClientId, UserConsentGrantType);

        await _pushNotificationService.PushSyncConnectedAppDeleteAsync(connection);
    }
}

#nullable enable

using Bit.Core.Auth.ConnectedApps.Commands.Interfaces;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.ConnectedApps.Commands;

public class RevokeConnectedAppCommand : IRevokeConnectedAppCommand
{
    private readonly IConnectedAppRepository _connectedAppRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public RevokeConnectedAppCommand(
        IConnectedAppRepository connectedAppRepository,
        IPushNotificationService pushNotificationService)
    {
        _connectedAppRepository = connectedAppRepository;
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
        await _pushNotificationService.PushSyncConnectedAppDeleteAsync(connection);
    }
}

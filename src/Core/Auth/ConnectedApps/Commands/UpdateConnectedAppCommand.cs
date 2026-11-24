#nullable enable

using Bit.Core.Auth.ConnectedApps.Commands.Interfaces;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.ConnectedApps.Commands;

public class UpdateConnectedAppCommand : IUpdateConnectedAppCommand
{
    private readonly IConnectedAppRepository _connectedAppRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public UpdateConnectedAppCommand(
        IConnectedAppRepository connectedAppRepository,
        IPushNotificationService pushNotificationService)
    {
        _connectedAppRepository = connectedAppRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<ConnectedApp> UpdateAsync(Guid userId, Guid connectionId, string name)
    {
        var connection = await _connectedAppRepository.GetByIdAsync(connectionId);
        if (connection == null || connection.UserId != userId)
        {
            throw new NotFoundException();
        }

        connection.Name = name;
        connection.RevisionDate = DateTime.UtcNow;

        await _connectedAppRepository.ReplaceAsync(connection);
        await _pushNotificationService.PushSyncConnectedAppUpdateAsync(connection);
        return connection;
    }
}

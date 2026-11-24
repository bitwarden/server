#nullable enable

using Bit.Core.Auth.ConnectedApps.Commands.Interfaces;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.ConnectedApps.Commands;

public class CreateConnectedAppCommand : ICreateConnectedAppCommand
{
    private readonly IConnectedAppRepository _connectedAppRepository;
    private readonly IOpenIdConnectPartnerStore _partnerStore;
    private readonly IPushNotificationService _pushNotificationService;

    public CreateConnectedAppCommand(
        IConnectedAppRepository connectedAppRepository,
        IOpenIdConnectPartnerStore partnerStore,
        IPushNotificationService pushNotificationService)
    {
        _connectedAppRepository = connectedAppRepository;
        _partnerStore = partnerStore;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<ConnectedApp> CreateAsync(Guid userId, ConnectedAppType type, string partnerClientId,
        string name, string encryptedKey)
    {
        // Bind to a known, vetted partner — never trust an arbitrary client-supplied id.
        var partner = _partnerStore.GetByClientId(partnerClientId)
            ?? throw new BadRequestException("Unknown partner client.");

        var now = DateTime.UtcNow;
        var connection = new ConnectedApp
        {
            UserId = userId,
            Type = type,
            PartnerClientId = partner.ClientId,
            Name = name,
            Key = encryptedKey,
            CreationDate = now,
            RevisionDate = now,
        };
        connection.SetNewId();

        await _connectedAppRepository.CreateAsync(connection);
        await _pushNotificationService.PushSyncConnectedAppCreateAsync(connection);
        return connection;
    }
}

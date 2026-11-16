#nullable enable

using Bit.Core.Auth.AgentFeatures.Commands.Interfaces;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.AgentFeatures.Commands;

public class CreateAgentConnectionCommand : ICreateAgentConnectionCommand
{
    private readonly IAgentConnectionRepository _agentConnectionRepository;
    private readonly IOpenIdConnectPartnerStore _partnerStore;
    private readonly IPushNotificationService _pushNotificationService;

    public CreateAgentConnectionCommand(
        IAgentConnectionRepository agentConnectionRepository,
        IOpenIdConnectPartnerStore partnerStore,
        IPushNotificationService pushNotificationService)
    {
        _agentConnectionRepository = agentConnectionRepository;
        _partnerStore = partnerStore;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<AgentConnection> CreateAsync(Guid userId, string partnerClientId, string name,
        string encryptedKey)
    {
        // Bind to a known, vetted partner — never trust an arbitrary client-supplied id.
        var partner = _partnerStore.GetByClientId(partnerClientId)
            ?? throw new BadRequestException("Unknown partner client.");

        var now = DateTime.UtcNow;
        var connection = new AgentConnection
        {
            UserId = userId,
            PartnerClientId = partner.ClientId,
            Name = name,
            Key = encryptedKey,
            CreationDate = now,
            RevisionDate = now,
        };
        connection.SetNewId();

        await _agentConnectionRepository.CreateAsync(connection);
        await _pushNotificationService.PushSyncAgentConnectionCreateAsync(connection);
        return connection;
    }
}

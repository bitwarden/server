#nullable enable

using Bit.Core.Auth.AgentFeatures.Commands.Interfaces;
using Bit.Core.Auth.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;

namespace Bit.Core.Auth.AgentFeatures.Commands;

public class RevokeAgentConnectionCommand : IRevokeAgentConnectionCommand
{
    private readonly IAgentConnectionRepository _agentConnectionRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public RevokeAgentConnectionCommand(
        IAgentConnectionRepository agentConnectionRepository,
        IPushNotificationService pushNotificationService)
    {
        _agentConnectionRepository = agentConnectionRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task RevokeAsync(Guid userId, Guid connectionId)
    {
        var connection = await _agentConnectionRepository.GetByIdAsync(connectionId);
        if (connection == null || connection.UserId != userId)
        {
            throw new NotFoundException();
        }

        await _agentConnectionRepository.DeleteAsync(connection);
        await _pushNotificationService.PushSyncAgentConnectionDeleteAsync(connection);
    }
}

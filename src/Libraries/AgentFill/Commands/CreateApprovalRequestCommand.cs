using Bit.AgentFill.Entities;
using Bit.AgentFill.Notifiers;
using Bit.AgentFill.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.AgentFill.Commands;

internal interface ICreateApprovalRequestCommand
{
    /// <summary>Stores a sealed approval request for five minutes and pushes its ID to the user's phones.</summary>
    Task<AgentFillApprovalRequest> CreateAsync(Guid userId, Guid requestDeviceId, string sealedRequest);
}

internal sealed class CreateApprovalRequestCommand(
    IAgentFillApprovalRequestRepository repository,
    IAgentFillRequestNotifier notifier,
    TimeProvider timeProvider,
    ILogger<CreateApprovalRequestCommand> logger) : ICreateApprovalRequestCommand
{
    public async Task<AgentFillApprovalRequest> CreateAsync(Guid userId, Guid requestDeviceId, string sealedRequest)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var request = new AgentFillApprovalRequest
        {
            Id = CombGuid.Generate(),
            UserId = userId,
            RequestDeviceId = requestDeviceId,
            SealedRequest = sealedRequest,
            CreationDate = now,
            ExpirationDate = now.Add(AgentFillApprovalRequest.Lifetime),
        };

        await repository.CreateAsync(request);
        await notifier.NotifyAsync(request);

        logger.LogInformation("Agent fill approval {ApprovalId} for {UserId}: {Outcome}",
            request.Id, userId, "Created");

        return request;
    }
}

using Bit.AgentFill.Entities;
using Bit.AgentFill.Repositories;
using Bit.Core.Platform.Push;
using Microsoft.Extensions.Logging;

namespace Bit.AgentFill.Commands;

internal enum AnswerOutcome
{
    Answered,
    AlreadyAnswered,
    Expired,
    NotFound,
}

/// <param name="Outcome">What happened to the answer.</param>
/// <param name="Request">The stored request after the attempt; <see langword="null"/> only for <see cref="AnswerOutcome.NotFound"/>.</param>
internal sealed record AnswerApprovalRequestResult(AnswerOutcome Outcome, AgentFillApprovalRequest? Request);

internal interface IAnswerApprovalRequestCommand
{
    /// <summary>
    /// Records the first sealed response to a request owned by <paramref name="userId"/>. Later or late responses
    /// are rejected without changing the stored record.
    /// </summary>
    Task<AnswerApprovalRequestResult> AnswerAsync(Guid id, Guid userId, Guid responseDeviceId, string sealedResponse);
}

internal sealed class AnswerApprovalRequestCommand(
    IAgentFillApprovalRequestRepository repository,
    IPushNotificationService pushNotificationService,
    TimeProvider timeProvider,
    ILogger<AnswerApprovalRequestCommand> logger) : IAnswerApprovalRequestCommand
{
    public async Task<AnswerApprovalRequestResult> AnswerAsync(Guid id, Guid userId, Guid responseDeviceId,
        string sealedResponse)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var updated = await repository.AnswerAsync(id, userId, sealedResponse, responseDeviceId, now);

        var current = await repository.GetByIdAsync(id, userId);
        if (current is null)
        {
            return new AnswerApprovalRequestResult(AnswerOutcome.NotFound, null);
        }

        AnswerOutcome outcome;
        if (updated == 1)
        {
            await pushNotificationService.PushAgentFillApprovalResponseAsync(current);
            outcome = AnswerOutcome.Answered;
        }
        else
        {
            outcome = current.IsAnswered ? AnswerOutcome.AlreadyAnswered : AnswerOutcome.Expired;
        }

        logger.LogInformation("Agent fill approval {ApprovalId} for {UserId}: {Outcome}", id, userId, outcome);
        return new AnswerApprovalRequestResult(outcome, current);
    }
}

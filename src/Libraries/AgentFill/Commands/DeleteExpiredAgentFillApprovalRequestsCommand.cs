using Bit.AgentFill.Repositories;

namespace Bit.AgentFill.Commands;

/// <summary>
/// Deletes agent fill approval requests that expired more than a day ago.
/// Called by the Admin host's scheduled job.
/// </summary>
public interface IDeleteExpiredAgentFillApprovalRequestsCommand
{
    /// <summary>Deletes the expired requests in one set-based operation. Safe to run repeatedly.</summary>
    /// <returns>The number of requests deleted.</returns>
    Task<int> RunAsync();
}

internal sealed class DeleteExpiredAgentFillApprovalRequestsCommand(
    IAgentFillApprovalRequestRepository repository,
    TimeProvider timeProvider) : IDeleteExpiredAgentFillApprovalRequestsCommand
{
    /// <summary>How long a request is kept after it expires.</summary>
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(1);

    public Task<int> RunAsync()
        => repository.DeleteExpiredAsync(timeProvider.GetUtcNow().UtcDateTime.Subtract(Retention));
}

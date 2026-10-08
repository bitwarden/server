using Bit.AgentFill.Entities;

namespace Bit.AgentFill.Repositories;

internal interface IAgentFillApprovalRequestRepository
{
    Task CreateAsync(AgentFillApprovalRequest request);

    /// <summary>Returns the request only when it belongs to <paramref name="userId"/>.</summary>
    Task<AgentFillApprovalRequest?> GetByIdAsync(Guid id, Guid userId);

    /// <summary>
    /// Records the response with a single conditional update: the request must belong to <paramref name="userId"/>,
    /// have no response yet and expire after <paramref name="responseDate"/>.
    /// </summary>
    /// <returns>The number of rows updated: 1 when this answer won, 0 otherwise.</returns>
    Task<int> AnswerAsync(Guid id, Guid userId, string sealedResponse, Guid responseDeviceId, DateTime responseDate);

    /// <summary>Deletes every request whose expiration date is before <paramref name="expiredBefore"/>.</summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteExpiredAsync(DateTime expiredBefore);
}

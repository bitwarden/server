using Bit.AgentFill.Entities;
using Bit.AgentFill.Repositories;

namespace Bit.AgentFill.Queries;

internal interface IGetApprovalRequestQuery
{
    /// <summary>Returns the request when it belongs to <paramref name="userId"/>; otherwise <see langword="null"/>.</summary>
    Task<AgentFillApprovalRequest?> GetAsync(Guid id, Guid userId);
}

internal sealed class GetApprovalRequestQuery(IAgentFillApprovalRequestRepository repository) : IGetApprovalRequestQuery
{
    public Task<AgentFillApprovalRequest?> GetAsync(Guid id, Guid userId) => repository.GetByIdAsync(id, userId);
}

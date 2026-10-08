using Bit.AgentFill.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfAgentFillApprovalRequest = Bit.Infrastructure.EntityFramework.AgentFill.Models.AgentFillApprovalRequest;

namespace Bit.AgentFill.Repositories;

internal sealed class EntityFrameworkAgentFillApprovalRequestRepository : IAgentFillApprovalRequestRepository
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EntityFrameworkAgentFillApprovalRequestRepository(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task CreateAsync(AgentFillApprovalRequest request)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        dbContext.AgentFillApprovalRequests.Add(new EfAgentFillApprovalRequest
        {
            Id = request.Id,
            UserId = request.UserId,
            RequestDeviceId = request.RequestDeviceId,
            SealedRequest = request.SealedRequest,
            CreationDate = request.CreationDate,
            ExpirationDate = request.ExpirationDate,
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task<AgentFillApprovalRequest?> GetByIdAsync(Guid id, Guid userId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.AgentFillApprovalRequests
            .AsNoTracking()
            .Where(r => r.Id == id && r.UserId == userId)
            .Select(r => new AgentFillApprovalRequest
            {
                Id = r.Id,
                UserId = r.UserId,
                RequestDeviceId = r.RequestDeviceId,
                SealedRequest = r.SealedRequest,
                SealedResponse = r.SealedResponse,
                ResponseDeviceId = r.ResponseDeviceId,
                CreationDate = r.CreationDate,
                ExpirationDate = r.ExpirationDate,
                ResponseDate = r.ResponseDate,
            })
            .SingleOrDefaultAsync();
    }

    public async Task<int> AnswerAsync(Guid id, Guid userId, string sealedResponse, Guid responseDeviceId,
        DateTime responseDate)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Matches AgentFillApprovalRequest_Answer: one conditional UPDATE, so concurrent answers store exactly one response.
        return await dbContext.AgentFillApprovalRequests
            .Where(r => r.Id == id
                        && r.UserId == userId
                        && r.SealedResponse == null
                        && r.ExpirationDate > responseDate)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.SealedResponse, sealedResponse)
                .SetProperty(r => r.ResponseDeviceId, responseDeviceId)
                .SetProperty(r => r.ResponseDate, responseDate));
    }

    public async Task<int> DeleteExpiredAsync(DateTime expiredBefore)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.AgentFillApprovalRequests
            .Where(r => r.ExpirationDate < expiredBefore)
            .ExecuteDeleteAsync();
    }
}

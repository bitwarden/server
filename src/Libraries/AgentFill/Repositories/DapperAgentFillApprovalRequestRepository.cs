using System.Data;
using Bit.AgentFill.Entities;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.AgentFill.Repositories;

internal sealed class DapperAgentFillApprovalRequestRepository : BaseRepository, IAgentFillApprovalRequestRepository
{
    public DapperAgentFillApprovalRequestRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public async Task CreateAsync(AgentFillApprovalRequest request)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[AgentFillApprovalRequest_Create]",
            new
            {
                request.Id,
                request.UserId,
                request.RequestDeviceId,
                request.SealedRequest,
                request.CreationDate,
                request.ExpirationDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<AgentFillApprovalRequest?> GetByIdAsync(Guid id, Guid userId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<AgentFillApprovalRequest>(
            "[dbo].[AgentFillApprovalRequest_ReadById]",
            new { Id = id, UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> AnswerAsync(Guid id, Guid userId, string sealedResponse, Guid responseDeviceId,
        DateTime responseDate)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "[dbo].[AgentFillApprovalRequest_Answer]",
            new
            {
                Id = id,
                UserId = userId,
                SealedResponse = sealedResponse,
                ResponseDeviceId = responseDeviceId,
                ResponseDate = responseDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> DeleteExpiredAsync(DateTime expiredBefore)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "[dbo].[AgentFillApprovalRequest_DeleteExpired]",
            new { ExpiredBefore = expiredBefore },
            commandType: CommandType.StoredProcedure);
    }
}

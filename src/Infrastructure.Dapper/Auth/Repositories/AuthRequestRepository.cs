﻿using System.Data;
using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class AuthRequestRepository : Repository<AuthRequest, Guid>, IAuthRequestRepository
{
    private readonly GlobalSettings _globalSettings;
    public AuthRequestRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        _globalSettings = globalSettings;
    }

    public async Task<int> DeleteExpiredAsync(
        TimeSpan userRequestExpiration, TimeSpan adminRequestExpiration, TimeSpan afterAdminApprovalExpiration)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.ExecuteAsync(
                $"[{Schema}].[AuthRequest_DeleteIfExpired]",
                new
                {
                    UserExpirationSeconds = (int)userRequestExpiration.TotalSeconds,
                    AdminExpirationSeconds = (int)adminRequestExpiration.TotalSeconds,
                    AdminApprovalExpirationSeconds = (int)afterAdminApprovalExpiration.TotalSeconds,
                },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<AuthRequest>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<AuthRequest>(
                $"[{Schema}].[AuthRequest_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<IEnumerable<PendingAuthRequestDetails>> GetManyPendingAuthRequestByUserId(Guid userId)
    {
        var expirationMinutes = (int)_globalSettings.PasswordlessAuth.UserRequestExpiration.TotalMinutes;
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PendingAuthRequestDetails>(
            $"[{Schema}].[AuthRequest_ReadPendingByUserId]",
            new { UserId = userId, ExpirationMinutes = expirationMinutes },
            commandType: CommandType.StoredProcedure);

        return results;
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationAdminAuthRequest>(
                $"[{Schema}].[AuthRequest_ReadPendingByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(Guid organizationId, IEnumerable<Guid> ids)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationAdminAuthRequest>(
                $"[{Schema}].[AuthRequest_ReadAdminApprovalsByIds]",
                new { OrganizationId = organizationId, Ids = ids.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task UpdateManyAsync(IEnumerable<AuthRequest> authRequests)
    {
        if (!authRequests.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[dbo].[AuthRequest_UpdateMany]",
                new { jsonData = JsonSerializer.Serialize(authRequests) },
                commandType: CommandType.StoredProcedure);
        }
    }
}

using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationInviteLinkRepository
    : Repository<OrganizationInviteLink, Guid>, IOrganizationInviteLinkRepository
{
    public OrganizationInviteLinkRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationInviteLinkRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<OrganizationInviteLink?> GetByCodeAsync(Guid code)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationInviteLink>(
            $"[{Schema}].[{Table}_ReadByCode]",
            new { Code = code },
            commandType: CommandType.StoredProcedure);
        return results.SingleOrDefault();
    }

    public async Task<OrganizationInviteLink?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationInviteLink>(
            $"[{Schema}].[{Table}_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);
        return results.SingleOrDefault();
    }

    public async Task RefreshAsync(OrganizationInviteLink oldLink, OrganizationInviteLink newLink)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_DeleteById]",
                new { Id = oldLink.Id },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);

            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_Create]",
                new
                {
                    newLink.Id,
                    newLink.Code,
                    newLink.OrganizationId,
                    newLink.AllowedDomains,
                    newLink.EncryptedInviteKey,
                    newLink.EncryptedOrgKey,
                    newLink.CreationDate,
                    newLink.RevisionDate,
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

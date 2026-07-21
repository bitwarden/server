using System.Data;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationInviteLinkRepository
    : Repository<OrganizationInviteLink, Guid>, IOrganizationInviteLinkRepository
{
    private readonly IDataProtector _dataProtector;

    public OrganizationInviteLinkRepository(
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
        : this(globalSettings.SqlServer.ConnectionString,
               globalSettings.SqlServer.ReadOnlyConnectionString,
               dataProtectionProvider)
    { }

    public OrganizationInviteLinkRepository(
        string connectionString,
        string readOnlyConnectionString,
        IDataProtectionProvider dataProtectionProvider)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<OrganizationInviteLink?> GetByIdAsync(Guid id)
    {
        var link = await base.GetByIdAsync(id);
        UnprotectData(link);
        return link;
    }

    public async Task<OrganizationInviteLink?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationInviteLink>(
            $"[{Schema}].[{Table}_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);
        var link = results.SingleOrDefault();
        UnprotectData(link);
        return link;
    }

    public override async Task<OrganizationInviteLink> CreateAsync(OrganizationInviteLink link)
    {
        await ProtectDataAndSaveAsync(link, () => base.CreateAsync(link));
        return link;
    }

    public override async Task ReplaceAsync(OrganizationInviteLink link)
    {
        await ProtectDataAndSaveAsync(link, () => base.ReplaceAsync(link));
    }

    public async Task RefreshAsync(OrganizationInviteLink oldLink, OrganizationInviteLink newLink)
    {
        var originalCode = newLink.Code;
        ProtectData(newLink);
        try
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
                        newLink.Invite,
                        newLink.SupportsConfirmation,
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
        finally
        {
            newLink.Code = originalCode;
        }
    }

    private async Task ProtectDataAndSaveAsync(OrganizationInviteLink link, Func<Task> saveTask)
    {
        var originalCode = link.Code;
        ProtectData(link);
        try
        {
            await saveTask();
        }
        finally
        {
            link.Code = originalCode;
        }
    }

    private void ProtectData(OrganizationInviteLink link)
    {
        if (!link.Code?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            link.Code = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(link.Code!));
        }
    }

    private void UnprotectData(OrganizationInviteLink? link)
    {
        if (link?.Code?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            link.Code = _dataProtector.Unprotect(
                link.Code.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }
}

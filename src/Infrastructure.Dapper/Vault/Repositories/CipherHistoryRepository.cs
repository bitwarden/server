// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Repositories;

public class CipherHistoryRepository : Repository<CipherHistory, Guid>, ICipherHistoryRepository
{
    public CipherHistoryRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CipherHistoryRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public override async Task<CipherHistory> GetByIdAsync(Guid id)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            return await connection.QuerySingleOrDefaultAsync<CipherHistory>(
                $"SELECT Id, CipherId, UserId, OrganizationId, Type, Data, Favorites, Folders, Attachments, CreationDate, RevisionDate, DeletedDate, Reprompt, [Key], ArchivedDate, HistoryDate FROM [{Schema}].[{Table}] WHERE Id = @Id",
                new { Id = id });
        }
    }

    public async Task<ICollection<CipherHistory>> GetManyByCipherIdAsync(Guid cipherId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<CipherHistory>(
                $"SELECT Id, CipherId, UserId, OrganizationId, Type, Data, Favorites, Folders, Attachments, CreationDate, RevisionDate, DeletedDate, Reprompt, [Key], ArchivedDate, HistoryDate FROM [{Schema}].[{Table}] WHERE CipherId = @CipherId ORDER BY HistoryDate DESC",
                new { CipherId = cipherId });

            return results.ToList();
        }
    }
}

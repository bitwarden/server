using System.Data;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class ProviderOrganizationRepository : Repository<ProviderOrganization, Guid>, IProviderOrganizationRepository
{
    public ProviderOrganizationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ProviderOrganizationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<ProviderOrganization>> CreateManyAsync(IEnumerable<ProviderOrganization> providerOrganizations)
    {
        var entities = providerOrganizations.ToList();

        if (!entities.Any())
        {
            return default;
        }

        foreach (var providerOrganization in entities)
        {
            providerOrganization.SetNewId();
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.DestinationTableName = "[dbo].[ProviderOrganization]";
                        var dataTable = BuildProviderOrganizationsTable(bulkCopy, entities);
                        await bulkCopy.WriteToServerAsync(dataTable);
                    }

                    transaction.Commit();

                    return entities.ToList();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public async Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganizationOrganizationDetails>(
                "[dbo].[ProviderOrganizationOrganizationDetails_ReadByProviderId]",
                new { ProviderId = providerId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ProviderOrganization> GetByOrganizationId(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganization>(
                "[dbo].[ProviderOrganization_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<IEnumerable<ProviderOrganizationProviderDetails>> GetManyByUserAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganizationProviderDetails>(
                "[dbo].[ProviderOrganizationProviderDetails_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<int> GetCountByOrganizationIdsAsync(
        IEnumerable<Guid> organizationIds)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                $"[{Schema}].[ProviderOrganization_ReadCountByOrganizationIds]",
                new { Ids = organizationIds.ToGuidIdArrayTVP() },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    private DataTable BuildProviderOrganizationsTable(SqlBulkCopy bulkCopy, IEnumerable<ProviderOrganization> providerOrganizations)
    {
        var po = providerOrganizations.FirstOrDefault();
        if (po == null)
        {
            throw new ApplicationException("Must have some ProviderOrganizations to bulk import.");
        }

        var providerOrganizationsTable = new DataTable("ProviderOrganizationDataTable");

        var idColumn = new DataColumn(nameof(po.Id), typeof(Guid));
        providerOrganizationsTable.Columns.Add(idColumn);
        var providerIdColumn = new DataColumn(nameof(po.ProviderId), typeof(Guid));
        providerOrganizationsTable.Columns.Add(providerIdColumn);
        var organizationIdColumn = new DataColumn(nameof(po.OrganizationId), typeof(Guid));
        providerOrganizationsTable.Columns.Add(organizationIdColumn);
        var keyColumn = new DataColumn(nameof(po.Key), typeof(string));
        providerOrganizationsTable.Columns.Add(keyColumn);
        var settingsColumn = new DataColumn(nameof(po.Settings), typeof(string));
        providerOrganizationsTable.Columns.Add(settingsColumn);
        var creationDateColumn = new DataColumn(nameof(po.CreationDate), po.CreationDate.GetType());
        providerOrganizationsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(po.RevisionDate), po.RevisionDate.GetType());
        providerOrganizationsTable.Columns.Add(revisionDateColumn);

        foreach (DataColumn col in providerOrganizationsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        providerOrganizationsTable.PrimaryKey = keys;

        foreach (var providerOrganization in providerOrganizations)
        {
            var row = providerOrganizationsTable.NewRow();

            row[idColumn] = providerOrganization.Id;
            row[providerIdColumn] = providerOrganization.ProviderId;
            row[organizationIdColumn] = providerOrganization.OrganizationId;
            row[keyColumn] = providerOrganization.Key;
            row[settingsColumn] = providerOrganization.Settings;
            row[creationDateColumn] = providerOrganization.CreationDate;
            row[revisionDateColumn] = providerOrganization.RevisionDate;

            providerOrganizationsTable.Rows.Add(row);
        }

        return providerOrganizationsTable;
    }
}

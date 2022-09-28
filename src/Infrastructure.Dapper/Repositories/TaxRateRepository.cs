using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class TaxRateRepository : Repository<TaxRate, string>, ITaxRateRepository
{
    public TaxRateRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public TaxRateRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<TaxRate>> SearchAsync(int skip, int count)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<TaxRate>(
                $"[{Schema}].[TaxRate_Search]",
                new { Skip = skip, Count = count },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<TaxRate>> GetAllActiveAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<TaxRate>(
                $"[{Schema}].[TaxRate_ReadAllActive]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task ArchiveAsync(TaxRate model)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[TaxRate_Archive]",
                new { Id = model.Id },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<TaxRate>> GetByLocationAsync(TaxRate model)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<TaxRate>(
                $"[{Schema}].[TaxRate_ReadByLocation]",
                new { Country = model.Country, PostalCode = model.PostalCode },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}

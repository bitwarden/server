using System.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public abstract class Repository<T, TId> : BaseRepository, IRepository<T, TId>
    where TId : IEquatable<TId>
    where T : class, ITableObject<TId>
{
    public Repository(
        string connectionString,
        string readOnlyConnectionString,
        string? schema = null,
        string? table = null
    )
        : base(connectionString, readOnlyConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(table))
        {
            Table = table;
        }

        if (!string.IsNullOrWhiteSpace(schema))
        {
            Schema = schema;
        }
    }

    protected string Schema { get; private set; } = "dbo";
    protected string Table { get; private set; } = typeof(T).Name;

    public virtual async Task<T?> GetByIdAsync(TId id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<T>(
                $"[{Schema}].[{Table}_ReadById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure
            );

            return results.SingleOrDefault();
        }
    }

    public virtual async Task<T> CreateAsync(T obj)
    {
        obj.SetNewId();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var parameters = new DynamicParameters();
            parameters.AddDynamicParams(obj);
            parameters.Add("Id", obj.Id, direction: ParameterDirection.InputOutput);
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_Create]",
                parameters,
                commandType: CommandType.StoredProcedure
            );
            obj.Id = parameters.Get<TId>(nameof(obj.Id));
        }
        return obj;
    }

    public virtual async Task ReplaceAsync(T obj)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_Update]",
                obj,
                commandType: CommandType.StoredProcedure
            );
        }
    }

    public virtual async Task UpsertAsync(T obj)
    {
        if (obj.Id.Equals(default(TId)))
        {
            await CreateAsync(obj);
        }
        else
        {
            await ReplaceAsync(obj);
        }
    }

    public virtual async Task DeleteAsync(T obj)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_DeleteById]",
                new { Id = obj.Id },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}

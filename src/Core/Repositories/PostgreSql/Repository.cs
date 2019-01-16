using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Bit.Core.Models.Table;
using Npgsql;

namespace Bit.Core.Repositories.PostgreSql
{
    public abstract class Repository<T, TId> : BasePostgreSqlRepository, IRepository<T, TId>
        where TId : IEquatable<TId>
        where T : class, ITableObject<TId>
    {
        public Repository(string connectionString, string readOnlyConnectionString, string table = null)
            : base(connectionString, readOnlyConnectionString)
        {
            if(!string.IsNullOrWhiteSpace(table))
            {
                Table = table;
            }
            else
            {
                Table = SnakeCase(typeof(T).Name).ToLowerInvariant();
            }
        }

        protected string Table { get; private set; }

        public virtual async Task<T> GetByIdAsync(TId id)
        {
            using(var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<T>(
                    $"{Table}_read_by_id",
                    new { id = id },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public virtual async Task CreateAsync(T obj)
        {
            obj.SetNewId();
            using(var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"{Table}_create",
                    obj,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            using(var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"{Table}_update",
                    obj,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public virtual async Task UpsertAsync(T obj)
        {
            if(obj.Id.Equals(default(TId)))
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
            using(var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"{Table}_delete_by_id",
                    new { id = obj.Id },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}

using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories.SqlServer.Models;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public abstract class Repository<T, TModel> : BaseRepository, IRepository<T> where T : IDataObject where TModel : ITableModel<T>
    {
        public Repository(string connectionString, string schema = null, string table = null)
            : base(connectionString)
        {
            if(!string.IsNullOrWhiteSpace(table))
            {
                Table = table;
            }

            if(!string.IsNullOrWhiteSpace(schema))
            {
                Schema = schema;
            }
        }

        protected string Schema { get; private set; } = "dbo";
        protected string Table { get; private set; } = typeof(T).Name;

        public virtual async Task<T> GetByIdAsync(string id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<TModel>(
                    $"[{Schema}].[{Table}_ReadById]",
                    new { Id = new Guid(id) },
                    commandType: CommandType.StoredProcedure);

                var model = results.FirstOrDefault();
                if(model == null)
                {
                    return default(T);
                }

                return model.ToDomain();
            }
        }

        public virtual async Task CreateAsync(T obj)
        {
            obj.Id = GenerateComb().ToString();
            var tableModel = (TModel)Activator.CreateInstance(typeof(TModel), obj);

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[{Table}_Create]",
                    tableModel,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            var tableModel = (TModel)Activator.CreateInstance(typeof(TModel), obj);

            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    $"[{Schema}].[{Table}_Update]",
                    tableModel,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public virtual async Task UpsertAsync(T obj)
        {
            if(string.IsNullOrWhiteSpace(obj.Id) || obj.Id == "0" || obj.Id == Guid.Empty.ToString())
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
            await DeleteByIdAsync(obj.Id);
        }

        public virtual async Task DeleteByIdAsync(string id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[{Table}_DeleteById]",
                    new { Id = new Guid(id) },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}

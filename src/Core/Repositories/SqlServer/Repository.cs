using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Repositories.SqlServer.Models;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public abstract class Repository<T, TModel> : IRepository<T> where T : IDataObject where TModel : ITableModel<T>
    {
        private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;

        public Repository(string connectionString, string schema = null, string table = null)
        {
            if(string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            ConnectionString = connectionString;

            if(!string.IsNullOrWhiteSpace(table))
            {
                Table = table;
            }

            if(!string.IsNullOrWhiteSpace(schema))
            {
                Schema = schema;
            }
        }

        protected string ConnectionString { get; private set; }
        protected string Schema { get; private set; } = "dbo";
        protected string Table { get; private set; } = typeof(T).Name;

        public virtual async Task<T> GetByIdAsync(string id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<TModel>(
                    $"[{Schema}].[{Table}_ReadById]",
                    new { Id = id },
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
            if(string.IsNullOrWhiteSpace(obj.Id))
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
                    new { Id = id },
                    commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// Generate sequential Guid for Sql Server.
        /// ref: https://github.com/nhibernate/nhibernate-core/blob/master/src/NHibernate/Id/GuidCombGenerator.cs
        /// </summary>
        /// <returns>A comb Guid.</returns>
        protected Guid GenerateComb()
        {
            byte[] guidArray = Guid.NewGuid().ToByteArray();

            var now = DateTime.UtcNow;

            // Get the days and milliseconds which will be used to build the byte string 
            var days = new TimeSpan(now.Ticks - _baseDateTicks);
            var msecs = now.TimeOfDay;

            // Convert to a byte array 
            // Note that SQL Server is accurate to 1/300th of a millisecond so we divide by 3.333333 
            var daysArray = BitConverter.GetBytes(days.Days);
            var msecsArray = BitConverter.GetBytes((long)(msecs.TotalMilliseconds / 3.333333));

            // Reverse the bytes to match SQL Servers ordering 
            Array.Reverse(daysArray);
            Array.Reverse(msecsArray);

            // Copy the bytes into the guid 
            Array.Copy(daysArray, daysArray.Length - 2, guidArray, guidArray.Length - 6, 2);
            Array.Copy(msecsArray, msecsArray.Length - 4, guidArray, guidArray.Length - 4, 4);

            return new Guid(guidArray);
        }
    }
}

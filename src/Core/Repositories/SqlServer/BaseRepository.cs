using System;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public abstract class BaseRepository
    {
        static BaseRepository()
        {
            SqlMapper.AddTypeHandler(new DateTimeHandler());
        }

        public BaseRepository(string connectionString)
        {
            if(string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            ConnectionString = connectionString;
        }

        protected string ConnectionString { get; private set; }
    }
}

using System;

namespace Bit.Core.Repositories.SqlServer
{
    public abstract class BaseRepository
    {
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

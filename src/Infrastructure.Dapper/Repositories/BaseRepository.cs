using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public abstract class BaseRepository
{
    static BaseRepository()
    {
        SqlMapper.AddTypeHandler(new DateTimeHandler());
    }

    public BaseRepository(string connectionString, string readOnlyConnectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }
        if (string.IsNullOrWhiteSpace(readOnlyConnectionString))
        {
            throw new ArgumentNullException(nameof(readOnlyConnectionString));
        }

        ConnectionString = connectionString;
        ReadOnlyConnectionString = readOnlyConnectionString;
    }

    protected string ConnectionString { get; private set; }
    protected string ReadOnlyConnectionString { get; private set; }
}

using System.Data.Common;
using Bit.Core.Platform.Data;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public abstract class BaseRepository
{
    static BaseRepository()
    {
        SqlMapper.AddTypeHandler(new DateTimeHandler());
        SqlMapper.AddTypeHandler(new JsonCollectionTypeHandler());
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

    /// <summary>
    /// Returns the ambient connection and transaction if an ambient transaction is active,
    /// or creates a new owned connection. The caller must dispose the connection only if
    /// <c>Owned</c> is true.
    /// </summary>
    protected (SqlConnection Connection, DbTransaction? Transaction, bool Owned) GetConnection()
    {
        var holder = TransactionState.Current;
        if (holder is not null)
        {
            return ((SqlConnection)holder.Connection, holder.Transaction, false);
        }

        return (new SqlConnection(ConnectionString), null, true);
    }

    /// <summary>
    /// Executes an action using the ambient transaction connection (if active) or a new
    /// owned connection. The connection is opened and disposed automatically when owned.
    /// </summary>
    protected async Task<TResult> ExecuteWithConnectionAsync<TResult>(
        Func<SqlConnection, DbTransaction?, Task<TResult>> action)
    {
        var (connection, transaction, owned) = GetConnection();
        try
        {
            if (owned)
            {
                await connection.OpenAsync();
            }

            return await action(connection, transaction);
        }
        finally
        {
            if (owned)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Executes an action using the ambient transaction connection (if active) or a new
    /// owned connection. The connection is opened and disposed automatically when owned.
    /// </summary>
    protected async Task ExecuteWithConnectionAsync(
        Func<SqlConnection, DbTransaction?, Task> action)
    {
        var (connection, transaction, owned) = GetConnection();
        try
        {
            if (owned)
            {
                await connection.OpenAsync();
            }

            await action(connection, transaction);
        }
        finally
        {
            if (owned)
            {
                await connection.DisposeAsync();
            }
        }
    }
}

using Microsoft.Data.SqlClient;

namespace Bit.Core.Repositories;

/// <summary>
/// A database operation that can optionally participate in an existing SQL connection and transaction.
/// Used to compose multiple repository operations into a single atomic transaction.
/// <para>Note: connection and transaction are only used for Dapper. They won't be available in EF.</para>
/// </summary>
public delegate Task DatabaseTransactionAction(SqlConnection? connection = null, SqlTransaction? transaction = null);

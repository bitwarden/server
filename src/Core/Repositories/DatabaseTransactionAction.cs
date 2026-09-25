using System.Data.Common;

namespace Bit.Core.Repositories;

/// <summary>
/// A database operation that participates in an existing database connection and transaction.
/// Used to compose multiple repository operations into a single atomic transaction.
/// </summary>
public delegate Task DatabaseTransactionAction(DbConnection connection, DbTransaction transaction);

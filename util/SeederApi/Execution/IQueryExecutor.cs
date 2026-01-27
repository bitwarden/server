using System.Text.Json;

namespace Bit.SeederApi.Execution;

/// <summary>
/// Executor for dynamically resolving and executing queries by name.
/// This is an infrastructure component that orchestrates query execution,
/// not a domain-level query.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a query with the given query name and arguments.
    /// Queries are read-only and do not track entities or create seed IDs.
    /// </summary>
    /// <param name="queryName">The name of the query (e.g., "EmergencyAccessInviteQuery")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the query's Execute method</param>
    /// <returns>The result of the query execution</returns>
    /// <exception cref="Services.QueryNotFoundException">Thrown when the query is not found</exception>
    /// <exception cref="Services.QueryExecutionException">Thrown when there's an error executing the query</exception>
    object Execute(string queryName, JsonElement? arguments);
}

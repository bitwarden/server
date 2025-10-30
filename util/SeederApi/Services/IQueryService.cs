﻿using System.Text.Json;

namespace Bit.SeederApi.Services;

public interface IQueryService
{
    /// <summary>
    /// Executes a query with the given query name and arguments.
    /// Queries are read-only and do not track entities or create seed IDs.
    /// </summary>
    /// <param name="queryName">The name of the query (e.g., "EmergencyAccessInviteQuery")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the query's Execute method</param>
    /// <returns>The result of the query execution</returns>
    /// <exception cref="SceneNotFoundException">Thrown when the query is not found</exception>
    /// <exception cref="SceneExecutionException">Thrown when there's an error executing the query</exception>
    object ExecuteQuery(string queryName, JsonElement? arguments);
}

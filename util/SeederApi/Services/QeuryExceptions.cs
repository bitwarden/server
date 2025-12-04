namespace Bit.SeederApi.Services;

public class QueryNotFoundException(string query) : Exception($"Query '{query}' not found");

public class QueryExecutionException : Exception
{
    public QueryExecutionException(string message) : base(message) { }
    public QueryExecutionException(string message, Exception innerException)
        : base(message, innerException) { }
}

using System.Text.Json;
using Bit.Seeder;

namespace Bit.SeederApi.Services;

public class QueryService(
    ILogger<QueryService> logger,
    IServiceProvider serviceProvider)
    : IQueryService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public object ExecuteQuery(string queryName, JsonElement? arguments)
    {
        try
        {
            var query = serviceProvider.GetKeyedService<IQuery>(queryName)
                ?? throw new QueryNotFoundException(queryName);

            var requestType = query.GetRequestType();

            // Deserialize the arguments into the request model
            object? requestModel;
            if (arguments == null)
            {
                // Try to create an instance with default values
                try
                {
                    requestModel = Activator.CreateInstance(requestType);
                    if (requestModel == null)
                    {
                        throw new QueryExecutionException(
                            $"Arguments are required for query '{queryName}'");
                    }
                }
                catch
                {
                    throw new QueryExecutionException(
                        $"Arguments are required for query '{queryName}'");
                }
            }
            else
            {
                try
                {
                    requestModel = JsonSerializer.Deserialize(arguments.Value.GetRawText(), requestType, _jsonOptions);
                    if (requestModel == null)
                    {
                        throw new QueryExecutionException(
                            $"Failed to deserialize request model for query '{queryName}'");
                    }
                }
                catch (JsonException ex)
                {
                    throw new QueryExecutionException(
                        $"Failed to deserialize request model for query '{queryName}': {ex.Message}", ex);
                }
            }

            var result = query.Execute(requestModel);

            logger.LogInformation("Successfully executed query: {QueryName}", queryName);
            return result;
        }
        catch (Exception ex) when (ex is not QueryNotFoundException and not QueryExecutionException)
        {
            logger.LogError(ex, "Unexpected error executing query: {QueryName}", queryName);
            throw new QueryExecutionException(
                $"An unexpected error occurred while executing query '{queryName}'",
                ex.InnerException ?? ex);
        }
    }
}

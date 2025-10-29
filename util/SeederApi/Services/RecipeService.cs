using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder;
using Bit.SeederApi.Models.Response;

namespace Bit.SeederApi.Services;

public class SeedService(
    DatabaseContext databaseContext,
    ILogger<SeedService> logger,
    IServiceProvider serviceProvider,
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository)
    : ISeedService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public List<SeededData> GetAllSeededData()
    {
        return databaseContext.SeededData.ToList();
    }

    public SceneResponseModel ExecuteScene(string templateName, JsonElement? arguments)
    {
        var result = ExecuteSceneMethod(templateName, arguments, "Seed");

        if (result.TrackedEntities.Count == 0)
        {
            return SceneResponseModel.FromSceneResult(result, seedId: null);
        }

        var seededData = new SeededData
        {
            Id = Guid.NewGuid(),
            RecipeName = templateName,
            Data = JsonSerializer.Serialize(result.TrackedEntities),
            CreationDate = DateTime.UtcNow
        };

        databaseContext.Add(seededData);
        databaseContext.SaveChanges();

        logger.LogInformation("Saved seeded data with ID {SeedId} for scene {RecipeName}",
            seededData.Id, templateName);

        return SceneResponseModel.FromSceneResult(result, seededData.Id);
    }

    public object ExecuteQuery(string queryName, JsonElement? arguments)
    {
        try
        {
            var query = serviceProvider.GetKeyedService<IQuery>(queryName)
                ?? throw new RecipeNotFoundException(queryName);

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
                        throw new RecipeExecutionException(
                            $"Arguments are required for query '{queryName}'");
                    }
                }
                catch
                {
                    throw new RecipeExecutionException(
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
                        throw new RecipeExecutionException(
                            $"Failed to deserialize request model for query '{queryName}'");
                    }
                }
                catch (JsonException ex)
                {
                    throw new RecipeExecutionException(
                        $"Failed to deserialize request model for query '{queryName}': {ex.Message}", ex);
                }
            }

            var result = query.Execute(requestModel);

            logger.LogInformation("Successfully executed query: {QueryName}", queryName);
            return result;
        }
        catch (Exception ex) when (ex is not RecipeNotFoundException and not RecipeExecutionException)
        {
            logger.LogError(ex, "Unexpected error executing query: {QueryName}", queryName);
            throw new RecipeExecutionException(
                $"An unexpected error occurred while executing query '{queryName}'",
                ex.InnerException ?? ex);
        }
    }

    public async Task<object?> DestroyRecipe(Guid seedId)
    {
        var seededData = databaseContext.SeededData.FirstOrDefault(s => s.Id == seedId);
        if (seededData == null)
        {
            logger.LogInformation("No seeded data found with ID {SeedId}, skipping", seedId);
            return null;
        }

        var trackedEntities = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(seededData.Data);
        if (trackedEntities == null)
        {
            throw new RecipeExecutionException($"Failed to deserialize tracked entities for seed ID {seedId}");
        }

        // Delete in reverse order to respect foreign key constraints
        if (trackedEntities.TryGetValue("User", out var userIds))
        {
            var users = databaseContext.Users.Where(u => userIds.Contains(u.Id));
            await userRepository.DeleteManyAsync(users);
        }

        if (trackedEntities.TryGetValue("Organization", out var orgIds))
        {
            var organizations = databaseContext.Organizations.Where(o => orgIds.Contains(o.Id));
            var aggregateException = new AggregateException();
            foreach (var org in organizations)
            {
                try
                {
                    await organizationRepository.DeleteAsync(org);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                }
            }
            if (aggregateException.InnerExceptions.Count > 0)
            {
                throw new RecipeExecutionException(
                    $"One or more errors occurred while deleting organizations for seed ID {seedId}",
                    aggregateException);
            }
        }

        databaseContext.Remove(seededData);
        databaseContext.SaveChanges();

        logger.LogInformation("Successfully destroyed seeded data with ID {SeedId} for scene {RecipeName}",
            seedId, seededData.RecipeName);

        return new { SeedId = seedId, RecipeName = seededData.RecipeName };
    }

    private SceneResult<object?> ExecuteSceneMethod(string templateName, JsonElement? arguments, string methodName)
    {
        try
        {
            var scene = serviceProvider.GetKeyedService<IScene>(templateName)
                ?? throw new RecipeNotFoundException(templateName);

            var requestType = scene.GetRequestType();

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
                        throw new RecipeExecutionException(
                            $"Arguments are required for scene '{templateName}'");
                    }
                }
                catch
                {
                    throw new RecipeExecutionException(
                        $"Arguments are required for scene '{templateName}'");
                }
            }
            else
            {
                try
                {
                    requestModel = JsonSerializer.Deserialize(arguments.Value.GetRawText(), requestType, _jsonOptions);
                    if (requestModel == null)
                    {
                        throw new RecipeExecutionException(
                            $"Failed to deserialize request model for scene '{templateName}'");
                    }
                }
                catch (JsonException ex)
                {
                    throw new RecipeExecutionException(
                        $"Failed to deserialize request model for scene '{templateName}': {ex.Message}", ex);
                }
            }

            var result = scene.Seed(requestModel);

            logger.LogInformation("Successfully executed {MethodName} on scene: {TemplateName}", methodName, templateName);
            return result;
        }
        catch (Exception ex) when (ex is not RecipeNotFoundException and not RecipeExecutionException)
        {
            logger.LogError(ex, "Unexpected error executing {MethodName} on scene: {TemplateName}", methodName, templateName);
            throw new RecipeExecutionException(
                $"An unexpected error occurred while executing {methodName} on scene '{templateName}'",
                ex.InnerException ?? ex);
        }
    }
}

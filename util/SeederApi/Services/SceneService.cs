using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder;
using Bit.SeederApi.Models.Response;

namespace Bit.SeederApi.Services;

public class SceneService(
    DatabaseContext databaseContext,
    ILogger<SceneService> logger,
    IServiceProvider serviceProvider,
    IUserRepository userRepository,
    IPlayDataRepository playDataRepository,
    IOrganizationRepository organizationRepository)
    : ISceneService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public List<string> GetAllPlayIds()
    {
        return databaseContext.PlayData
            .Select(pd => pd.PlayId)
            .Distinct()
            .ToList();
    }

    public async Task<SceneResponseModel> ExecuteScene(string templateName, JsonElement? arguments)
    {
        var result = await ExecuteSceneMethodAsync(templateName, arguments, "Seed");

        return SceneResponseModel.FromSceneResult(result);
    }

    public async Task<object?> DestroyScene(string playId)
    {
        // Note, delete cascade will remove PlayData entries

        var playData = await playDataRepository.GetByPlayIdAsync(playId);
        var userIds = playData.Select(pd => pd.UserId).Distinct().ToList();
        var organizationIds = playData.Select(pd => pd.OrganizationId).Distinct().ToList();

        // Delete Users before Organizations to respect foreign key constraints
        if (userIds.Count > 0)
        {
            var users = databaseContext.Users.Where(u => userIds.Contains(u.Id));
            await userRepository.DeleteManyAsync(users);
        }

        if (organizationIds.Count > 0)
        {
            var organizations = databaseContext.Organizations.Where(o => organizationIds.Contains(o.Id));
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
                throw new SceneExecutionException(
                    $"One or more errors occurred while deleting organizations for seed ID {playId}",
                    aggregateException);
            }
        }

        logger.LogInformation("Successfully destroyed seeded data with ID {PlayId}",
            playId);

        return new { PlayId = playId };
    }

    public async Task DestroyScenes(IEnumerable<string> playIds)
    {
        var exceptions = new List<Exception>();

        var deleteTasks = playIds.Select(async playId =>
        {
            try
            {
                await DestroyScene(playId);
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
                logger.LogError(ex, "Error deleting seeded data: {PlayId}", playId);
            }
        });

        await Task.WhenAll(deleteTasks);

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more errors occurred while deleting seeded data", exceptions);
        }
    }

    private async Task<SceneResult<object?>> ExecuteSceneMethodAsync(string templateName, JsonElement? arguments, string methodName)
    {
        try
        {
            var scene = serviceProvider.GetKeyedService<IScene>(templateName)
                ?? throw new SceneNotFoundException(templateName);

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
                        throw new SceneExecutionException(
                            $"Arguments are required for scene '{templateName}'");
                    }
                }
                catch
                {
                    throw new SceneExecutionException(
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
                        throw new SceneExecutionException(
                            $"Failed to deserialize request model for scene '{templateName}'");
                    }
                }
                catch (JsonException ex)
                {
                    throw new SceneExecutionException(
                        $"Failed to deserialize request model for scene '{templateName}': {ex.Message}", ex);
                }
            }

            var result = await scene.SeedAsync(requestModel);

            logger.LogInformation("Successfully executed {MethodName} on scene: {TemplateName}", methodName, templateName);
            return result;
        }
        catch (Exception ex) when (ex is not SceneNotFoundException and not SceneExecutionException)
        {
            logger.LogError(ex, "Unexpected error executing {MethodName} on scene: {TemplateName}", methodName, templateName);
            throw new SceneExecutionException(
                $"An unexpected error occurred while executing {methodName} on scene '{templateName}'",
                ex.InnerException ?? ex);
        }
    }
}

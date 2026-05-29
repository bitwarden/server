using System.Text.Json;
using Bit.Seeder;
using Bit.SeederApi.Models.Response;
using Bit.SeederApi.Services;

namespace Bit.SeederApi.Execution;

public class SceneExecutor(
    ILogger<SceneExecutor> logger,
    IServiceProvider serviceProvider) : ISceneExecutor
{

    public async Task<SceneResponseModel> ExecuteAsync(string templateName, JsonElement? arguments)
    {
        try
        {
            var scene = serviceProvider.GetKeyedService<IScene>(templateName)
                ?? throw new SceneNotFoundException(templateName);

            var requestType = scene.GetRequestType();
            var requestModel = DeserializeRequestModel(templateName, requestType, arguments);
            var result = await scene.SeedAsync(requestModel);

            logger.LogInformation("Successfully executed scene: {TemplateName}", templateName);
            return SceneResponseModel.FromSceneResult(result);
        }
        catch (Exception ex) when (ex is not SceneNotFoundException and not SceneExecutionException)
        {
            logger.LogError(ex, "Unexpected error executing scene: {TemplateName}", templateName);
            throw new SceneExecutionException(
                $"An unexpected error occurred while executing scene '{templateName}'",
                ex.InnerException ?? ex);
        }
    }

    private object DeserializeRequestModel(string templateName, Type requestType, JsonElement? arguments)
    {
        if (arguments == null)
        {
            return CreateDefaultRequestModel(templateName, requestType);
        }

        try
        {
            var requestModel = JsonSerializer.Deserialize(arguments.Value.GetRawText(), requestType, JsonConfiguration.Options);
            if (requestModel == null)
            {
                throw new SceneExecutionException(
                    $"Failed to deserialize request model for scene '{templateName}'");
            }
            return requestModel;
        }
        catch (JsonException ex)
        {
            throw new SceneExecutionException(
                $"Failed to deserialize request model for scene '{templateName}': {ex.Message}", ex);
        }
    }

    private object CreateDefaultRequestModel(string templateName, Type requestType)
    {
        try
        {
            var requestModel = Activator.CreateInstance(requestType);
            if (requestModel == null)
            {
                throw new SceneExecutionException(
                    $"Arguments are required for scene '{templateName}'");
            }
            return requestModel;
        }
        catch
        {
            throw new SceneExecutionException(
                $"Arguments are required for scene '{templateName}'");
        }
    }
}

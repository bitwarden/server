using Bit.Infrastructure.EntityFramework.Repositories;
using System.Reflection;
using System.Text.Json;

namespace Bit.SeederApi.Services;

public class RecipeService : IRecipeService
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(DatabaseContext databaseContext, ILogger<RecipeService> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    public object? ExecuteRecipe(string templateName, JsonElement? arguments)
    {
        try
        {
            var recipeType = LoadRecipeType(templateName);
            var seedMethod = GetSeedMethod(recipeType, templateName);
            var recipeInstance = Activator.CreateInstance(recipeType, _databaseContext)!;

            var methodArguments = ParseMethodArguments(seedMethod, arguments);
            var result = seedMethod.Invoke(recipeInstance, methodArguments);

            _logger.LogInformation("Successfully executed recipe: {TemplateName}", templateName);
            return result;
        }
        catch (Exception ex) when (ex is not RecipeNotFoundException and not RecipeExecutionException)
        {
            _logger.LogError(ex, "Unexpected error executing recipe: {TemplateName}", templateName);
            throw new RecipeExecutionException(
                $"An unexpected error occurred while executing recipe '{templateName}'",
                ex.InnerException ?? ex);
        }
    }

    private static Type LoadRecipeType(string templateName)
    {
        var recipeTypeName = $"Bit.Seeder.Recipes.{templateName}";
        var recipeType = Assembly.Load("Seeder")
            .GetTypes()
            .FirstOrDefault(t => t.FullName == recipeTypeName);

        return recipeType ?? throw new RecipeNotFoundException(templateName);
    }

    private static MethodInfo GetSeedMethod(Type recipeType, string templateName)
    {
        var seedMethod = recipeType.GetMethod("Seed");
        return seedMethod ?? throw new RecipeExecutionException($"Seed method not found in recipe '{templateName}'");
    }

    private static object?[] ParseMethodArguments(MethodInfo seedMethod, JsonElement? arguments)
    {
        var parameters = seedMethod.GetParameters();

        if (arguments == null && parameters.Length > 0)
        {
            throw new RecipeExecutionException("Arguments are required for this recipe");
        }

        var methodArguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterName = parameter.Name!;

            if (arguments?.TryGetProperty(parameterName, out var value) == true)
            {
                try
                {
                    methodArguments[i] = JsonSerializer.Deserialize(value.GetRawText(), parameter.ParameterType);
                }
                catch (JsonException ex)
                {
                    throw new RecipeExecutionException(
                        $"Failed to deserialize parameter '{parameterName}': {ex.Message}", ex);
                }
            }
            else if (!parameter.HasDefaultValue)
            {
                throw new RecipeExecutionException($"Missing required parameter: {parameterName}");
            }
            else
            {
                methodArguments[i] = parameter.DefaultValue;
            }
        }

        return methodArguments;
    }
}

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
            // Find the recipe class
            var recipeTypeName = $"Bit.Seeder.Recipes.{templateName}";
            var recipeType = Assembly.Load("Seeder")
                .GetTypes()
                .FirstOrDefault(t => t.FullName == recipeTypeName);

            if (recipeType == null)
            {
                throw new RecipeNotFoundException($"Recipe '{templateName}' not found");
            }

            // Instantiate the recipe with DatabaseContext
            var recipeInstance = Activator.CreateInstance(recipeType, _databaseContext);
            if (recipeInstance == null)
            {
                throw new RecipeExecutionException("Failed to instantiate recipe");
            }

            // Find the Seed method
            var seedMethod = recipeType.GetMethod("Seed");
            if (seedMethod == null)
            {
                throw new RecipeExecutionException($"Seed method not found in recipe '{templateName}'");
            }

            // Parse arguments and match to method parameters
            var parameters = seedMethod.GetParameters();
            var methodArguments = new object?[parameters.Length];

            if (arguments == null && parameters.Length > 0)
            {
                throw new RecipeExecutionException("Arguments are required for this recipe");
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterName = parameter.Name!;

                if (arguments?.TryGetProperty(parameterName, out JsonElement value) == true)
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

            // Invoke the Seed method
            var result = seedMethod.Invoke(recipeInstance, methodArguments);
            _logger.LogInformation("Successfully executed recipe: {TemplateName}", templateName);

            return result;
        }
        catch (RecipeNotFoundException)
        {
            throw;
        }
        catch (RecipeExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing recipe: {TemplateName}", templateName);
            throw new RecipeExecutionException(
                $"An unexpected error occurred while executing recipe '{templateName}'",
                ex.InnerException ?? ex);
        }
    }
}

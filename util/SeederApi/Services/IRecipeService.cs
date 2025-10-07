using System.Text.Json;

namespace Bit.SeederApi.Services;

public interface IRecipeService
{
    /// <summary>
    /// Executes a recipe with the given template name and arguments.
    /// </summary>
    /// <param name="templateName">The name of the recipe template (e.g., "OrganizationWithUsersRecipe")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the recipe's Seed method</param>
    /// <returns>The result returned by the recipe's Seed method</returns>
    /// <exception cref="RecipeNotFoundException">Thrown when the recipe template is not found</exception>
    /// <exception cref="RecipeExecutionException">Thrown when there's an error executing the recipe</exception>
    object? ExecuteRecipe(string templateName, JsonElement? arguments);

    /// <summary>
    /// Destroys data created by a recipe with the given template name and arguments.
    /// </summary>
    /// <param name="templateName">The name of the recipe template (e.g., "OrganizationWithUsersRecipe")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the recipe's Destroy method</param>
    /// <returns>The result returned by the recipe's Destroy method</returns>
    /// <exception cref="RecipeNotFoundException">Thrown when the recipe template is not found</exception>
    /// <exception cref="RecipeExecutionException">Thrown when there's an error executing the recipe</exception>
    object? DestroyRecipe(string templateName, JsonElement? arguments);
}

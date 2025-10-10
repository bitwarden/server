using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.SeederApi.Services;

public interface IRecipeService
{
    /// <summary>
    /// Executes a recipe with the given template name and arguments.
    /// </summary>
    /// <param name="templateName">The name of the recipe template (e.g., "OrganizationWithUsersRecipe")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the recipe's Seed method</param>
    /// <returns>A tuple containing the result and optional seed ID for tracked entities</returns>
    /// <exception cref="RecipeNotFoundException">Thrown when the recipe template is not found</exception>
    /// <exception cref="RecipeExecutionException">Thrown when there's an error executing the recipe</exception>
    (object? Result, Guid? SeedId) ExecuteRecipe(string templateName, JsonElement? arguments);

    /// <summary>
    /// Destroys data created by a recipe using the seeded data ID.
    /// </summary>
    /// <param name="seedId">The ID of the seeded data to destroy</param>
    /// <returns>The result of the destroy operation</returns>
    /// <exception cref="RecipeExecutionException">Thrown when there's an error destroying the seeded data</exception>
    Task<object?> DestroyRecipe(Guid seedId);
    List<SeededData> GetAllSeededData();
}

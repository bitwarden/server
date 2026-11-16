using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Entry point extension method for registering recipes on <see cref="IServiceCollection"/>.
/// </summary>
public static class RecipeServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Creates a new <see cref="RecipeBuilder"/> for registering steps as keyed services.
        /// </summary>
        /// <param name="recipeName">Unique name used as the keyed service key</param>
        /// <returns>A new RecipeBuilder for fluent step registration</returns>
        public RecipeBuilder AddRecipe(string recipeName)
        {
            return new RecipeBuilder(recipeName, services);
        }
    }
}

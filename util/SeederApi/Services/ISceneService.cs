using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.SeederApi.Models.Response;

namespace Bit.SeederApi.Services;

public interface ISceneService
{
    /// <summary>
    /// Executes a scene with the given template name and arguments.
    /// </summary>
    /// <param name="templateName">The name of the scene template (e.g., "SingleUserScene")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the scene's Seed method</param>
    /// <returns>A tuple containing the result and optional seed ID for tracked entities</returns>
    /// <exception cref="SceneNotFoundException">Thrown when the scene template is not found</exception>
    /// <exception cref="SceneExecutionException">Thrown when there's an error executing the scene</exception>
    SceneResponseModel ExecuteScene(string templateName, JsonElement? arguments);

    /// <summary>
    /// Destroys data created by a scene using the seeded data ID.
    /// </summary>
    /// <param name="seedId">The ID of the seeded data to destroy</param>
    /// <returns>The result of the destroy operation</returns>
    /// <exception cref="SceneExecutionException">Thrown when there's an error destroying the seeded data</exception>
    Task<object?> DestroyScene(Guid seedId);
    List<SeededData> GetAllSeededData();
}

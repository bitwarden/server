using System.Text.Json;
using Bit.SeederApi.Models.Response;

namespace Bit.SeederApi.Services;

/// <summary>
/// Service for executing and managing scene operations.
/// </summary>
/// <remarks>
/// The scene service provides a mechanism to execute scene operations by name with optional JSON arguments.
/// Scenes create and configure test data, track entities for cleanup, and support destruction of seeded data.
/// Each scene execution can be assigned a play ID for tracking and subsequent cleanup operations.
/// </remarks>
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
    Task<SceneResponseModel> ExecuteScene(string templateName, JsonElement? arguments);

    /// <summary>
    /// Destroys data created by a scene using the seeded data ID.
    /// </summary>
    /// <param name="playId">The ID of the seeded data to destroy</param>
    /// <returns>The result of the destroy operation</returns>
    /// <exception cref="SceneExecutionException">Thrown when there's an error destroying the seeded data</exception>
    Task<object?> DestroyScene(string playId);

    /// <summary>
    /// Retrieves all play IDs for currently tracked seeded data.
    /// </summary>
    /// <returns>A list of play IDs representing active seeded data that can be destroyed.</returns>
    List<string> GetAllPlayIds();

    /// <summary>
    /// Destroys multiple scenes by their play IDs.
    /// </summary>
    /// <param name="playIds">The list of play IDs to destroy</param>
    /// <exception cref="AggregateException">Thrown when one or more scenes fail to destroy</exception>
    Task DestroyScenes(IEnumerable<string> playIds);
}

namespace Bit.SeederApi.Commands.Interfaces;

/// <summary>
/// Command for destroying data created by a single scene.
/// </summary>
public interface IDestroySceneCommand
{
    /// <summary>
    /// Destroys data created by a scene using the seeded data ID.
    /// </summary>
    /// <param name="playId">The ID of the seeded data to destroy</param>
    /// <returns>The result of the destroy operation</returns>
    /// <exception cref="Services.SceneExecutionException">Thrown when there's an error destroying the seeded data</exception>
    Task<object?> DestroyAsync(string playId);
}

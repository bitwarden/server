using System.Text.Json;
using Bit.SeederApi.Models.Response;

namespace Bit.SeederApi.Execution;

/// <summary>
/// Executor for dynamically resolving and executing scenes by template name.
/// This is an infrastructure component that orchestrates scene execution,
/// not a domain-level command.
/// </summary>
public interface ISceneExecutor
{
    /// <summary>
    /// Executes a scene with the given template name and arguments.
    /// </summary>
    /// <param name="templateName">The name of the scene template (e.g., "SingleUserScene")</param>
    /// <param name="arguments">Optional JSON arguments to pass to the scene's Seed method</param>
    /// <returns>A scene response model containing the result and mangle map</returns>
    /// <exception cref="Services.SceneNotFoundException">Thrown when the scene template is not found</exception>
    /// <exception cref="Services.SceneExecutionException">Thrown when there's an error executing the scene</exception>
    Task<SceneResponseModel> ExecuteAsync(string templateName, JsonElement? arguments);
}

namespace Bit.Seeder;

/// <summary>
/// Base interface for seeding operations. The base interface should not be used directly, rather use `IScene&lt;Request&gt;`.
/// </summary>
/// <remarks>
/// Scenes are components in the seeding system that create and configure test data. They follow
/// a type-safe pattern using generics to ensure proper request/response handling while maintaining
/// a common non-generic interface for dynamic invocation.
/// </remarks>
public interface IScene
{
    /// <summary>
    /// Gets the type of request this scene expects.
    /// </summary>
    /// <returns>The request type that this scene can process.</returns>
    Type GetRequestType();

    /// <summary>
    /// Seeds data based on the provided request object.
    /// </summary>
    /// <param name="request">The request object containing parameters for the seeding operation.</param>
    /// <returns>A scene result containing any returned data, mangle map, and entity tracking information.</returns>
    Task<SceneResult<object?>> SeedAsync(object request);
}

/// <summary>
/// Generic scene interface for seeding operations with a specific request type. Does not return a value beyond tracking entities and a mangle map.
/// </summary>
/// <typeparam name="TRequest">The type of request object this scene accepts.</typeparam>
/// <remarks>
/// Use this interface when your scene needs to process a specific request type but doesn't need to
/// return any data beyond the standard mangle map for ID transformations and entity tracking.
/// The explicit interface implementations allow this scene to be invoked dynamically through the
/// base IScene interface while maintaining type safety in the implementation.
/// </remarks>
public interface IScene<TRequest> : IScene where TRequest : class
{
    /// <summary>
    /// Seeds data based on the provided strongly-typed request.
    /// </summary>
    /// <param name="request">The request object containing parameters for the seeding operation.</param>
    /// <returns>A scene result containing the mangle map and entity tracking information.</returns>
    Task<SceneResult> SeedAsync(TRequest request);

    /// <summary>
    /// Gets the request type for this scene.
    /// </summary>
    /// <returns>The type of TRequest.</returns>
    Type IScene.GetRequestType() => typeof(TRequest);

    /// <summary>
    /// Adapts the non-generic SeedAsync to the strongly-typed version.
    /// </summary>
    /// <param name="request">The request object to cast and process.</param>
    /// <returns>A scene result wrapped as an object result.</returns>
    async Task<SceneResult<object?>> IScene.SeedAsync(object request)
    {
        var result = await SeedAsync((TRequest)request);
        return new SceneResult(mangleMap: result.MangleMap);
    }
}

/// <summary>
/// Generic scene interface for seeding operations with a specific request type that returns typed data.
/// </summary>
/// <typeparam name="TRequest">The type of request object this scene accepts. Must be a reference type.</typeparam>
/// <typeparam name="TResult">The type of data this scene returns. Must be a reference type.</typeparam>
/// <remarks>
/// Use this interface when your scene needs to return specific data that can be used by subsequent
/// scenes or test logic. The result is wrapped in a SceneResult that also includes the mangle map
/// and entity tracking information. The explicit interface implementations allow dynamic invocation
/// while preserving type safety in the implementation.
/// </remarks>
public interface IScene<TRequest, TResult> : IScene where TRequest : class where TResult : class
{
    /// <summary>
    /// Seeds data based on the provided strongly-typed request and returns typed result data.
    /// </summary>
    /// <param name="request">The request object containing parameters for the seeding operation.</param>
    /// <returns>A scene result containing the typed result data, mangle map, and entity tracking information.</returns>
    Task<SceneResult<TResult>> SeedAsync(TRequest request);

    /// <summary>
    /// Gets the request type for this scene.
    /// </summary>
    /// <returns>The type of TRequest.</returns>
    Type IScene.GetRequestType() => typeof(TRequest);

    /// <summary>
    /// Adapts the non-generic SeedAsync to the strongly-typed version.
    /// </summary>
    /// <param name="request">The request object to cast and process.</param>
    /// <returns>A scene result with the typed result cast to object.</returns>
    async Task<SceneResult<object?>> IScene.SeedAsync(object request) => (SceneResult<object?>)await SeedAsync((TRequest)request);
}

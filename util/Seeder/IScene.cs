namespace Bit.Seeder;

public interface IScene
{
    Type GetRequestType();
    Task<SceneResult<object?>> SeedAsync(object request);
}

/// <summary>
/// Generic scene interface for seeding operations with a specific request type. Does not return a value beyond tracking entities and a mangle map.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
public interface IScene<TRequest> : IScene where TRequest : class
{
    Task<SceneResult> SeedAsync(TRequest request);
    Type IScene.GetRequestType() => typeof(TRequest);
    async Task<SceneResult<object?>> IScene.SeedAsync(object request)
    {
        var result = await SeedAsync((TRequest)request);
        return new SceneResult(mangleMap: result.MangleMap);
    }
}

public interface IScene<TRequest, TResult> : IScene where TRequest : class where TResult : class
{
    Task<SceneResult<TResult>> SeedAsync(TRequest request);

    Type IScene.GetRequestType() => typeof(TRequest);
    async Task<SceneResult<object?>> IScene.SeedAsync(object request) => (SceneResult<object?>)await SeedAsync((TRequest)request);
}

namespace Bit.Seeder;

public interface IScene
{
    Type GetRequestType();
    SceneResult<object?> Seed(object request);
}

/// <summary>
/// Generic scene interface for seeding operations with a specific request type. Does not return a value beyond tracking entities and a mangle map.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
public interface IScene<TRequest> : IScene where TRequest : class
{
    SceneResult Seed(TRequest request);
    Type IScene.GetRequestType() => typeof(TRequest);
    SceneResult<object?> IScene.Seed(object request)
    {
        var result = Seed((TRequest)request);
        return new SceneResult(mangleMap: result.MangleMap, trackedEntities: result.TrackedEntities);
    }
}

public interface IScene<TRequest, TResult> : IScene where TRequest : class where TResult : class
{
    SceneResult<TResult> Seed(TRequest request);

    Type IScene.GetRequestType() => typeof(TRequest);
    SceneResult<object?> IScene.Seed(object request) => (SceneResult<object?>)Seed((TRequest)request);
}

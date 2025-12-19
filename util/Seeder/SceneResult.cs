namespace Bit.Seeder;

/// <summary>
/// Helper for exposing a <see cref="IScene" /> interface with a SeedAsync method.
/// </summary>
public class SceneResult(Dictionary<string, string?> mangleMap)
    : SceneResult<object?>(result: null, mangleMap: mangleMap);

/// <summary>
/// Generic result from executing a Scene.
/// Contains custom scene-specific data and a mangle map that maps magic strings from the
/// request to their mangled (collision-free) values inserted into the database.
/// </summary>
/// <typeparam name="TResult">The type of custom result data returned by the scene.</typeparam>
public class SceneResult<TResult>(TResult result, Dictionary<string, string?> mangleMap)
{
    public TResult Result { get; init; } = result;
    public Dictionary<string, string?> MangleMap { get; init; } = mangleMap;

    public static explicit operator SceneResult<object?>(SceneResult<TResult> v)
    {
        var result = v.Result;

        return result is null
            ? new SceneResult<object?>(result: null, mangleMap: v.MangleMap)
            : new SceneResult<object?>(result: result, mangleMap: v.MangleMap);
    }
}

namespace Bit.Seeder;

public class SceneResult(Dictionary<string, string?> mangleMap)
    : SceneResult<object?>(result: null, mangleMap: mangleMap);

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

namespace Bit.Seeder;

public class SceneResult : SceneResult<object?>
{
    public SceneResult(Dictionary<string, string?> mangleMap)
        : base(result: null, mangleMap: mangleMap) { }
}

public class SceneResult<TResult>
{
    public TResult Result { get; init; }
    public Dictionary<string, string?> MangleMap { get; init; }

    public SceneResult(TResult result, Dictionary<string, string?> mangleMap)
    {
        Result = result;
        MangleMap = mangleMap;
    }

    public static explicit operator SceneResult<object?>(SceneResult<TResult> v)
    {
        var result = v.Result;

        if (result is null)
        {
            return new SceneResult<object?>(result: null, mangleMap: v.MangleMap);
        }
        else
        {
            return new SceneResult<object?>(result: result, mangleMap: v.MangleMap);
        }
    }
}

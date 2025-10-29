namespace Bit.Seeder;

public class SceneResult : SceneResult<object?>
{
    public SceneResult(Dictionary<string, string?> mangleMap, Dictionary<string, List<Guid>> trackedEntities)
        : base(result: null, mangleMap: mangleMap, trackedEntities: trackedEntities) { }
}

public class SceneResult<TResult>
{
    public TResult Result { get; init; }
    public Dictionary<string, string?> MangleMap { get; init; }
    public Dictionary<string, List<Guid>> TrackedEntities { get; init; }

    public SceneResult(TResult result, Dictionary<string, string?> mangleMap, Dictionary<string, List<Guid>> trackedEntities)
    {
        Result = result;
        MangleMap = mangleMap;
        TrackedEntities = trackedEntities;
    }

    public static explicit operator SceneResult<object?>(SceneResult<TResult> v)
    {
        var result = v.Result;

        if (result is null)
        {
            return new SceneResult<object?>(result: null, mangleMap: v.MangleMap, trackedEntities: v.TrackedEntities);
        }
        else
        {
            return new SceneResult<object?>(result: result, mangleMap: v.MangleMap, trackedEntities: v.TrackedEntities);
        }
    }
}

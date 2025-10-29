using Bit.Seeder;

namespace Bit.SeederApi.Models.Response;

public class SceneResponseModel
{
    public required Guid? SeedId { get; init; }
    public required Dictionary<string, string?>? MangleMap { get; init; }
    public required object? Result { get; init; }

    public static SceneResponseModel FromSceneResult<T>(SceneResult<T> sceneResult, Guid? seedId)
    {
        return new SceneResponseModel
        {
            Result = sceneResult.Result,
            MangleMap = sceneResult.MangleMap,
            SeedId = seedId
        };
    }
}

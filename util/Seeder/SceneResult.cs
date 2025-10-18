namespace Bit.Seeder;

public class SceneResult
{
    public required object Result { get; init; }
    public Dictionary<string, List<Guid>> TrackedEntities { get; init; } = new();
}

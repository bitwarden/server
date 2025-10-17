namespace Bit.Seeder;

public class RecipeResult
{
    public required object Result { get; init; }
    public Dictionary<string, List<Guid>> TrackedEntities { get; init; } = new();
}

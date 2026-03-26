using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Seeds a standalone individual user from an embedded preset or programmatic options.
/// </summary>
/// <remarks>
/// Thin facade over the internal Pipeline architecture (RecipeOrchestrator).
/// All orchestration logic is encapsulated within the Pipeline, keeping this Recipe simple.
/// The CLI remains "dumb" - it creates this recipe and calls Seed().
/// </remarks>
public class IndividualUserRecipe(SeederDependencies deps)
{
    private readonly RecipeOrchestrator _orchestrator = new(deps);

    /// <summary>
    /// Seeds an individual user from an embedded preset.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "individual.free")</param>
    /// <param name="password">Optional password for the seeded account</param>
    /// <param name="kdfIterations">Optional KDF iteration count override</param>
    /// <returns>The user ID and summary statistics.</returns>
    public IndividualSeedResult Seed(string presetName,
        string? password = null, int? kdfIterations = null)
    {
        var result = _orchestrator.Execute(presetName, password, kdfIterations);
        return ToResult(result);
    }

    /// <summary>
    /// Seeds an individual user from programmatic options (CLI arguments).
    /// </summary>
    /// <param name="options">Options specifying what to seed.</param>
    /// <returns>The user ID and summary statistics.</returns>
    public IndividualSeedResult Seed(IndividualUserOptions options)
    {
        var result = _orchestrator.Execute(options);
        return ToResult(result);
    }

    private static IndividualSeedResult ToResult(ExecutionResult result) =>
        new(result.UserId!.Value, result.OwnerEmail, result.Premium, result.CiphersCount, result.FoldersCount);
}

/// <summary>
/// Result of individual user seeding with summary statistics.
/// </summary>
public record IndividualSeedResult(
    Guid UserId, string? Email, bool Premium, int CiphersCount, int FoldersCount);

using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Seeds an organization from an embedded preset or programmatic options.
/// </summary>
/// <remarks>
/// Thin facade over the internal Pipeline architecture (RecipeOrchestrator).
/// All orchestration logic is encapsulated within the Pipeline, keeping this Recipe simple.
/// The CLI remains "dumb" - it creates this recipe and calls Seed().
/// </remarks>
public class OrganizationRecipe(SeederDependencies deps)
{
    private readonly RecipeOrchestrator _orchestrator = new(deps);

    /// <summary>
    /// Seeds an organization from an embedded preset.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <param name="password">Optional password for all seeded accounts</param>
    /// <param name="kdfIterations">Optional KDF iteration count override</param>
    /// <returns>The organization ID and summary statistics.</returns>
    public OrganizationSeedResult Seed(string presetName, string? password = null, int? kdfIterations = null)
    {
        var result = _orchestrator.Execute(presetName, password, kdfIterations);

        if (result.OrganizationId is null)
        {
            throw new InvalidOperationException(
                $"Preset '{presetName}' is not an organization preset. Use IndividualUserRecipe instead.");
        }

        return OrganizationSeedResult.From(result);
    }

    /// <summary>
    /// Seeds an organization from programmatic options (CLI arguments).
    /// </summary>
    /// <param name="options">Options specifying what to seed.</param>
    /// <returns>The organization ID and summary statistics.</returns>
    public OrganizationSeedResult Seed(OrganizationVaultOptions options)
    {
        var result = _orchestrator.Execute(options);
        return OrganizationSeedResult.From(result);
    }
}

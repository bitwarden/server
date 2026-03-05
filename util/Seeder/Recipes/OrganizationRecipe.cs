using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Seeds an organization from an embedded preset or programmatic options.
/// </summary>
/// <remarks>
/// Thin facade over the internal Pipeline architecture (RecipeOrchestrator).
/// All orchestration logic is encapsulated within the Pipeline, keeping this Recipe simple.
/// The CLI remains "dumb" - it creates this recipe and calls Seed().
/// </remarks>
public class OrganizationRecipe(
    DatabaseContext db,
    IMapper mapper,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService)
{
    private readonly RecipeOrchestrator _orchestrator = new(db, mapper);

    /// <summary>
    /// Seeds an organization from an embedded preset.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <param name="password">Optional password for all seeded accounts</param>
    /// <returns>The organization ID and summary statistics.</returns>
    public SeedResult Seed(string presetName, string? password = null)
    {
        var result = _orchestrator.Execute(presetName, passwordHasher, manglerService, password);

        return new SeedResult(
            result.OrganizationId,
            result.OwnerEmail,
            result.UsersCount,
            result.GroupsCount,
            result.CollectionsCount,
            result.CiphersCount);
    }

    /// <summary>
    /// Seeds an organization from programmatic options (CLI arguments).
    /// </summary>
    /// <param name="options">Options specifying what to seed.</param>
    /// <returns>The organization ID and summary statistics.</returns>
    public SeedResult Seed(OrganizationVaultOptions options)
    {
        var result = _orchestrator.Execute(options, passwordHasher, manglerService);

        return new SeedResult(
            result.OrganizationId,
            result.OwnerEmail,
            result.UsersCount,
            result.GroupsCount,
            result.CollectionsCount,
            result.CiphersCount);
    }

    /// <summary>
    /// Lists all available embedded presets and fixtures.
    /// </summary>
    /// <returns>Available presets grouped by category.</returns>
    public static AvailableSeeds ListAvailable()
    {
        var internalResult = RecipeOrchestrator.ListAvailable();

        return new AvailableSeeds(internalResult.Presets, internalResult.Fixtures);
    }
}

/// <summary>
/// Result of seeding operation with summary statistics.
/// </summary>
public record SeedResult(
    Guid OrganizationId,
    string? OwnerEmail,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount);

/// <summary>
/// Available presets and fixtures grouped by category.
/// </summary>
public record AvailableSeeds(
    IReadOnlyList<string> Presets,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fixtures);

using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

/// <summary>
/// Seeds an organization from an embedded preset.
/// </summary>
/// <remarks>
/// This recipe is a thin facade over the internal Pipeline architecture (PresetExecutor).
/// All orchestration logic is encapsulated within the Pipeline, keeping this Recipe simple.
/// The CLI remains "dumb" - it creates this recipe and calls Seed().
/// </remarks>
public class OrganizationFromPresetRecipe(
    DatabaseContext db,
    IMapper mapper,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService)
{
    private readonly PresetExecutor _executor = new(db, mapper);

    /// <summary>
    /// Seeds an organization from an embedded preset.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <returns>The organization ID and summary statistics.</returns>
    public SeedResult Seed(string presetName)
    {
        var result = _executor.Execute(presetName, passwordHasher, manglerService);

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
        var internalResult = PresetExecutor.ListAvailable();

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

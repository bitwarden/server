using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Orchestrates preset-based seeding by coordinating the Pipeline infrastructure.
/// </summary>
internal sealed class PresetExecutor(DatabaseContext db, IMapper mapper)
{
    /// <summary>
    /// Executes a preset by loading it, building the pipeline, and executing all steps.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <param name="passwordHasher">Password hasher for user creation</param>
    /// <param name="manglerService">Mangler service for test isolation</param>
    /// <returns>Execution result with organization ID and entity counts</returns>
    internal ExecutionResult Execute(
        string presetName,
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService)
    {
        using var serviceProvider = BuildServiceProvider(passwordHasher, manglerService);
        var context = new SeederContext(serviceProvider);
        var committer = new BulkCommitter(db, mapper);
        var executor = new RecipeExecutor(committer);

        var builder = PresetLoader.Load(presetName, serviceProvider.GetRequiredService<ISeedReader>());
        var steps = builder.Build();

        return executor.Execute(steps, context);
    }

    /// <summary>
    /// Lists all available embedded presets and fixtures.
    /// </summary>
    /// <returns>Available presets grouped by category</returns>
    internal static AvailableSeeds ListAvailable()
    {
        var seedReader = new SeedReader();
        var all = seedReader.ListAvailable();

        var presets = all.Where(n => n.StartsWith("presets."))
            .Select(n => n["presets.".Length..])
            .ToList();

        var fixtures = all.Where(n => !n.StartsWith("presets."))
            .GroupBy(n => n.Split('.')[0])
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.ToList());

        return new AvailableSeeds(presets, fixtures);
    }

    private ServiceProvider BuildServiceProvider(
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(passwordHasher);
        services.AddSingleton(manglerService);
        services.AddSingleton<ISeedReader, SeedReader>();
        services.AddSingleton(db);
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Result of pipeline execution with organization ID and entity counts.
/// </summary>
internal record ExecutionResult(
    Guid OrganizationId,
    string? OwnerEmail,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount);

/// <summary>
/// Available presets and fixtures grouped by category.
/// </summary>
internal record AvailableSeeds(
    IReadOnlyList<string> Presets,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Fixtures);

using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Orchestrates recipe-based seeding by coordinating the Pipeline infrastructure.
/// </summary>
internal sealed class RecipeOrchestrator(DatabaseContext db, IMapper mapper)
{
    /// <summary>
    /// Executes a preset by registering its recipe, building a service provider, and running all steps.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <param name="passwordHasher">Password hasher for user creation</param>
    /// <param name="manglerService">Mangler service for test isolation</param>
    /// <param name="password">Optional password for all seeded accounts</param>
    /// <returns>Execution result with organization ID and entity counts</returns>
    internal ExecutionResult Execute(
        string presetName,
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService,
        string? password = null)
    {
        var reader = new SeedReader();

        var services = new ServiceCollection();
        services.AddSingleton(passwordHasher);
        services.AddSingleton(manglerService);
        services.AddSingleton<ISeedReader>(reader);
        services.AddSingleton(new SeederSettings(password));
        services.AddSingleton(db);

        PresetLoader.RegisterRecipe(presetName, reader, services);

        using var serviceProvider = services.BuildServiceProvider();
        var committer = new BulkCommitter(db, mapper);
        var executor = new RecipeExecutor(presetName, serviceProvider, committer);

        return executor.Execute();
    }

    /// <summary>
    /// Executes a recipe built programmatically from CLI options.
    /// </summary>
    internal ExecutionResult Execute(
        OrganizationVaultOptions options,
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(passwordHasher);
        services.AddSingleton(manglerService);
        services.AddSingleton(new SeederSettings(options.Password));

        var recipeName = "from-options";
        var builder = services.AddRecipe(recipeName);

        builder.CreateOrganization(options.Name, options.Domain, options.Users + 1, options.PlanType);
        builder.AddOwner();
        builder.WithGenerator(options.Domain);
        builder.AddUsers(options.Users, options.RealisticStatusMix);

        if (options.Groups > 0)
        {
            builder.AddGroups(options.Groups);
        }

        if (options.StructureModel.HasValue)
        {
            builder.AddCollections(options.StructureModel.Value);
        }
        else if (options.Ciphers > 0)
        {
            builder.AddCollections(1);
        }

        if (options.Ciphers > 0)
        {
            builder.AddFolders();
            builder.AddCiphers(options.Ciphers, options.CipherTypeDistribution, options.PasswordDistribution);
        }

        builder.Validate();

        using var serviceProvider = services.BuildServiceProvider();
        var committer = new BulkCommitter(db, mapper);
        var executor = new RecipeExecutor(recipeName, serviceProvider, committer);

        return executor.Execute();
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

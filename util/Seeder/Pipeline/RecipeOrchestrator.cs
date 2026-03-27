using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Orchestrates recipe-based seeding by coordinating the Pipeline infrastructure.
/// </summary>
internal sealed class RecipeOrchestrator(SeederDependencies deps)
{
    /// <summary>
    /// Executes a preset by registering its recipe, building a service provider, and running all steps.
    /// </summary>
    /// <param name="presetName">Name of the embedded preset (e.g., "dunder-mifflin-full")</param>
    /// <param name="password">Optional password for all seeded accounts</param>
    /// <param name="kdfIterations">Optional KDF iteration count. Defaults to 5,000 for fast seeding.</param>
    /// <returns>Execution result with organization ID and entity counts</returns>
    internal PipelineExecutionResult Execute(
        string presetName,
        string? password = null,
        int? kdfIterations = null)
    {
        var reader = new SeedReader();

        // Read preset to extract kdfIterations before building services.
        // CLI --kdf-iterations takes precedence over the preset value.
        var preset = reader.Read<Models.SeedPreset>($"presets.{presetName}");
        var effectiveKdf = kdfIterations ?? preset.KdfIterations ?? 5_000;

        var services = new ServiceCollection();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton<ISeedReader>(reader);
        services.AddSingleton(new SeederSettings(password, effectiveKdf));
        services.AddSingleton(deps.Db);

        PresetLoader.RegisterRecipe(presetName, reader, services);

        return BuildAndExecute(presetName, services);
    }

    /// <summary>
    /// Executes a recipe built programmatically from CLI options.
    /// </summary>
    internal PipelineExecutionResult Execute(OrganizationVaultOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton(new SeederSettings(options.Password, options.KdfIterations));

        var recipeName = "from-options";
        var builder = services.AddRecipe(recipeName);

        builder.CreateOrganization(options.Name, options.Domain, options.Users + 1, options.PlanType);
        builder.AddOrganizationApiKey();
        builder.AddOwner();
        builder.WithGenerator(options.Domain);
        builder.AddUsers(options.Users, options.RealisticStatusMix);

        if (options.Groups > 0)
        {
            builder.AddGroups(options.Groups, options.Density);
        }

        if (options.StructureModel.HasValue)
        {
            builder.AddCollections(options.StructureModel.Value);
        }
        else if (options.Collections > 0)
        {
            builder.AddCollections(options.Collections, options.Density);
        }
        else if (options.Ciphers > 0)
        {
            builder.AddCollections(1, options.Density);
        }

        if (options.Ciphers > 0)
        {
            builder.AddFolders(options.Density);
            builder.AddCiphers(options.Ciphers, options.CipherTypeDistribution, options.PasswordDistribution, density: options.Density);
        }

        builder.Validate();

        return BuildAndExecute(recipeName, services);
    }

    /// <summary>
    /// Executes a recipe for an individual user built programmatically from CLI options.
    /// </summary>
    internal PipelineExecutionResult Execute(IndividualUserOptions options)
    {
        var firstName = options.FirstName ?? new Bogus.Faker().Name.FirstName();
        var lastName = options.LastName ?? new Bogus.Faker().Name.LastName();
        var email = $"{firstName}.{lastName}@individual.example".ToLowerInvariant();

        var premium = options.Premium;
        var maxStorageGb = premium ? (short)1 : (short)0;

        var services = new ServiceCollection();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton(new SeederSettings(options.Password, options.KdfIterations));

        var recipeName = "individual-from-options";
        var builder = services.AddRecipe(recipeName);

        builder.CreateIndividualUser(email, premium, maxStorageGb);
        builder.WithGenerator("individual.example");

        if (options.GenerateVault)
        {
            builder.AddNamedFolders(["Social", "Finance", "Work", "Shopping", "Entertainment"]);
            builder.AddPersonalCiphers(75);
        }

        builder.Validate();

        return BuildAndExecute(recipeName, services);
    }

    private PipelineExecutionResult BuildAndExecute(string recipeName, ServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();
        var committer = new BulkCommitter(deps.Db, deps.Mapper);
        var executor = new RecipeExecutor(recipeName, serviceProvider, committer);
        return executor.Execute();
    }

}

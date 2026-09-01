using Bit.Core.Billing.Enums;
using Bit.Seeder.Factories;
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
    /// <param name="orgNameOverride">Optional organization name. Replaces the fixture/preset-supplied name when provided.</param>
    /// <param name="ownerEmailOverride">Optional owner email. Replaces the default <c>owner@&lt;domain&gt;</c> when provided.</param>
    /// <param name="stripeBilling">When set, creates real Stripe test-environment billing for the organization.</param>
    /// <returns>Execution result with organization ID and entity counts</returns>
    internal async Task<PipelineExecutionResult> ExecuteAsync(
        string presetName,
        string? password = null,
        int? kdfIterations = null,
        string? orgNameOverride = null,
        string? ownerEmailOverride = null,
        StripeBillingOptions? stripeBilling = null)
    {
        EnsureOwnerEmailUnique(
            ownerEmailOverride,
            deps.ManglerService.IsEnabled,
            email => deps.Db.Users.Any(u => u.Email == email));

        var reader = new SeedReader();

        // Read preset to extract kdfIterations before building services.
        // CLI --kdf-iterations takes precedence over the preset value.
        var preset = reader.Read<Models.SeedPreset>($"presets.{presetName}");

        // Resolved from the preset the same way CreateOrganizationStep will resolve it, so the Free-plan
        // rejection sees the plan that would actually be seeded. Still ahead of every write.
        if (stripeBilling is not null)
        {
            ValidateBillingOptIn(PlanFeatures.Parse(preset.Organization?.PlanType));
        }

        var effectiveKdf = kdfIterations ?? preset.KdfIterations ?? 5_000;

        var services = new ServiceCollection();
        services.AddSingleton(deps.LoggerFactory);
        services.AddLogging();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton(deps.AttachmentStorageService);
        services.AddSingleton<ISeedReader>(reader);
        services.AddSingleton(new SeederSettings(password, effectiveKdf, orgNameOverride, ownerEmailOverride));
        services.AddSingleton(deps.Db);
        if (deps.Progress is not null)
        {
            services.AddSingleton(deps.Progress);
        }
        if (stripeBilling is not null)
        {
            services.AddSingleton(deps.BillingInitializer!());
        }

        PresetLoader.RegisterRecipe(presetName, reader, services, stripeBilling);

        return await BuildAndExecuteAsync(presetName, services);
    }

    /// <summary>
    /// Executes a recipe built programmatically from CLI options.
    /// </summary>
    internal async Task<PipelineExecutionResult> ExecuteAsync(OrganizationVaultOptions options)
    {
        if (options.StripeBilling is not null)
        {
            ValidateBillingOptIn(options.PlanType);
        }

        EnsureOwnerEmailUnique(
            options.OwnerEmail,
            deps.ManglerService.IsEnabled,
            email => deps.Db.Users.Any(u => u.Email == email));

        var services = new ServiceCollection();
        services.AddSingleton(deps.LoggerFactory);
        services.AddLogging();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton(deps.AttachmentStorageService);
        services.AddSingleton(new SeederSettings(
            options.Password,
            options.KdfIterations,
            OrgNameOverride: null,
            OwnerEmailOverride: options.OwnerEmail));
        if (deps.Progress is not null)
        {
            services.AddSingleton(deps.Progress);
        }
        if (options.StripeBilling is not null)
        {
            services.AddSingleton(deps.BillingInitializer!());
        }

        var recipeName = "from-options";
        var builder = services.AddRecipe(recipeName);

        builder.CreateOrganization(options.Name, options.Domain, options.Users + 1, options.PlanType, options.Overrides);
        builder.AddOrganizationApiKey();

        if (options.ClaimedDomains.Count > 0)
        {
            builder.WithOrganizationDomain(options.ClaimedDomains);
        }

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

        if (options.StripeBilling is not null)
        {
            builder.WithStripeBilling(options.StripeBilling);
        }

        builder.Validate();

        return await BuildAndExecuteAsync(recipeName, services);
    }

    /// <summary>
    /// Executes a recipe for an individual user built programmatically from CLI options.
    /// </summary>
    internal async Task<PipelineExecutionResult> ExecuteAsync(IndividualUserOptions options)
    {
        var firstName = options.FirstName ?? new Bogus.Faker().Name.FirstName();
        var lastName = options.LastName ?? new Bogus.Faker().Name.LastName();
        var email = options.Email ?? $"{firstName}.{lastName}@individual.example".ToLowerInvariant();

        var premium = options.Premium;
        var maxStorageGb = premium ? (short)1 : (short)0;

        var services = new ServiceCollection();
        services.AddSingleton(deps.LoggerFactory);
        services.AddLogging();
        services.AddSingleton(deps.PasswordHasher);
        services.AddSingleton(deps.ManglerService);
        services.AddSingleton(deps.AttachmentStorageService);
        services.AddSingleton(new SeederSettings(options.Password, options.KdfIterations));
        services.AddSingleton(deps.LicensingService);
        services.AddSingleton(deps.LicenseSigner);
        if (deps.Progress is not null)
        {
            services.AddSingleton(deps.Progress);
        }

        var recipeName = "individual-from-options";
        var builder = services.AddRecipe(recipeName);

        DateTime? creationDate = options.AccountAgeDays > 0
            ? DateTime.UtcNow.AddDays(-options.AccountAgeDays)
            : null;

        builder.CreateIndividualUser(email, premium, maxStorageGb, options.SelfHosted, creationDate);
        builder.WithGenerator("individual.example");

        if (options.GenerateVault)
        {
            builder.AddNamedFolders(["Social", "Finance", "Work", "Shopping", "Entertainment"]);
            builder.AddPersonalCiphers(75);
        }

        builder.Validate();

        return await BuildAndExecuteAsync(recipeName, services);
    }

    private async Task<PipelineExecutionResult> BuildAndExecuteAsync(string recipeName, ServiceCollection services)
    {
        await using var serviceProvider = services.BuildServiceProvider();
        var committer = new BulkCommitter(deps.Db, deps.Mapper);
        var executor = new RecipeExecutor(recipeName, serviceProvider, committer);
        return await executor.ExecuteAsync();
    }

    /// <summary>
    /// Fails fast on a Stripe billing opt-in the host cannot honor, <strong>before any entity is created</strong>.
    /// </summary>
    /// <remarks>
    /// Billing runs in a post-commit step, so without this gate an unusable Stripe configuration would only
    /// surface after the organization and its users were already written to the database.
    /// </remarks>
    private void ValidateBillingOptIn(PlanType planType)
    {
        if (deps.BillingInitializer is null)
        {
            throw new InvalidOperationException(
                "Stripe billing was requested but no IStripeBillingInitializer was supplied on " +
                "SeederDependencies. The host has to register the billing services before opting in.");
        }

        deps.BillingInitializer().ValidateConfiguration(planType);
    }

    /// <summary>
    /// Fails fast when <c>--owner-email</c> resolves to a User.Email that already exists, producing an
    /// actionable error instead of a SQL unique-constraint exception from the BulkCommitter.
    /// </summary>
    /// <remarks>
    /// Skipped when mangling is enabled — the mangler prepends a per-run unique tag, so collisions are
    /// effectively impossible regardless of the override value.
    /// </remarks>
    internal static void EnsureOwnerEmailUnique(
        string? ownerEmailOverride,
        bool manglingEnabled,
        Func<string, bool> userExists)
    {
        if (manglingEnabled || string.IsNullOrWhiteSpace(ownerEmailOverride))
        {
            return;
        }

        if (userExists(ownerEmailOverride))
        {
            throw new InvalidOperationException(
                $"A User with email '{ownerEmailOverride}' already exists in the database. " +
                "Choose a different --owner-email, delete the existing user, or add --mangle for test isolation.");
        }
    }
}

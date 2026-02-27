using Bit.Core.Billing.Enums;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Step registration extension methods for <see cref="RecipeBuilder"/>.
/// Each method validates constraints, sets validation flags, and registers the step via DI.
/// </summary>
public static class RecipeBuilderExtensions
{
    /// <summary>
    /// Use an organization from embedded fixtures with optional plan/seats overrides from the preset.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Organization fixture name without extension</param>
    /// <param name="planType">Optional plan type override (from preset)</param>
    /// <param name="seats">Optional seats override (from preset)</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder UseOrganization(this RecipeBuilder builder, string fixture, string? planType = null, int? seats = null)
    {
        builder.HasOrg = true;
        builder.AddStep(_ => CreateOrganizationStep.FromFixture(fixture, planType, seats));
        return builder;
    }

    /// <summary>
    /// Create an organization inline with specified parameters.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="name">Organization display name</param>
    /// <param name="domain">Organization domain (used for email generation)</param>
    /// <param name="seats">Number of user seats</param>
    /// <param name="planType">Billing plan type (defaults to EnterpriseAnnually)</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder CreateOrganization(this RecipeBuilder builder, string name, string domain, int? seats = null, PlanType planType = PlanType.EnterpriseAnnually)
    {
        builder.HasOrg = true;
        builder.AddStep(_ => CreateOrganizationStep.FromParams(name, domain, seats, planType));
        return builder;
    }

    /// <summary>
    /// Add an organization owner user with admin privileges.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder AddOwner(this RecipeBuilder builder)
    {
        builder.HasOwner = true;
        builder.AddStep(_ => new CreateOwnerStep());
        return builder;
    }

    /// <summary>
    /// Initialize seeded random generator for reproducible test data.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="domain">Organization domain (used for seeding randomness)</param>
    /// <param name="seed">Optional explicit seed. If null, domain hash is used.</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder WithGenerator(this RecipeBuilder builder, string domain, int? seed = null)
    {
        builder.HasGenerator = true;
        builder.AddStep(_ => InitGeneratorStep.FromDomain(domain, seed));
        return builder;
    }

    /// <summary>
    /// Use a roster from embedded fixtures (users, groups, collections).
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Roster fixture name without extension</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <param name="reader">Seed reader for peeking the roster fixture to detect owner declarations</param>
    /// <exception cref="InvalidOperationException">Thrown when AddUsers() was already called</exception>
    public static RecipeBuilder UseRoster(this RecipeBuilder builder, string fixture, ISeedReader reader)
    {
        if (builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Cannot call UseRoster() after AddUsers(). Choose one user source.");
        }

        builder.HasRosterUsers = true;

        var roster = reader.Read<SeedRoster>($"rosters.{fixture}");
        if (roster.Users.Any(u => string.Equals(u.Role, "owner", StringComparison.OrdinalIgnoreCase)))
        {
            builder.HasRosterOwner = true;
        }

        builder.AddStep(_ => new CreateRosterStep(fixture));
        return builder;
    }

    /// <summary>
    /// Generate users with seeded random data.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of users to generate</param>
    /// <param name="realisticStatusMix">If true, includes revoked/invited users; if false, all confirmed</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseRoster() was already called</exception>
    public static RecipeBuilder AddUsers(this RecipeBuilder builder, int count, bool realisticStatusMix = false)
    {
        if (builder.HasRosterUsers)
        {
            throw new InvalidOperationException(
                "Cannot call AddUsers() after UseRoster(). Choose one user source.");
        }

        builder.HasGeneratedUsers = true;
        builder.AddStep(_ => new CreateUsersStep(count, realisticStatusMix));
        return builder;
    }

    /// <summary>
    /// Generate groups with random members from existing users.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of groups to generate</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddGroups(this RecipeBuilder builder, int count)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Groups require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => new CreateGroupsStep(count));
        return builder;
    }

    /// <summary>
    /// Generate collections with random assignments.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of collections to generate</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddCollections(this RecipeBuilder builder, int count)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => CreateCollectionsStep.FromCount(count));
        return builder;
    }

    /// <summary>
    /// Generate collections based on organizational structure model.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="structure">Organizational structure (Traditional, Spotify, Modern)</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddCollections(this RecipeBuilder builder, OrgStructureModel structure)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => CreateCollectionsStep.FromStructure(structure));
        return builder;
    }

    /// <summary>
    /// Generate folders for each user using a realistic distribution.
    /// </summary>
    public static RecipeBuilder AddFolders(this RecipeBuilder builder)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Folders require users. Call UseRoster() or AddUsers() first.");
        }

        builder.HasFolders = true;
        builder.AddStep(_ => new GenerateFoldersStep());
        return builder;
    }

    /// <summary>
    /// Use ciphers from embedded fixtures.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Cipher fixture name without extension</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when AddCiphers() was already called</exception>
    public static RecipeBuilder UseCiphers(this RecipeBuilder builder, string fixture)
    {
        if (builder.HasGeneratedCiphers)
        {
            throw new InvalidOperationException(
                "Cannot call UseCiphers() after AddCiphers(). Choose one cipher source.");
        }

        builder.HasFixtureCiphers = true;
        builder.AddStep(_ => new CreateCiphersStep(fixture));
        return builder;
    }

    /// <summary>
    /// Generate ciphers with configurable type and password strength distributions.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of ciphers to generate</param>
    /// <param name="typeDist">Distribution of cipher types. Uses realistic defaults if null.</param>
    /// <param name="pwDist">Distribution of password strengths. Uses realistic defaults if null.</param>
    /// <param name="assignFolders">When true, assigns ciphers to user folders round-robin.</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseCiphers() was already called</exception>
    public static RecipeBuilder AddCiphers(
        this RecipeBuilder builder,
        int count,
        Distribution<CipherType>? typeDist = null,
        Distribution<PasswordStrength>? pwDist = null,
        bool assignFolders = false)
    {
        if (builder.HasFixtureCiphers)
        {
            throw new InvalidOperationException(
                "Cannot call AddCiphers() after UseCiphers(). Choose one cipher source.");
        }

        builder.HasGeneratedCiphers = true;
        if (assignFolders)
        {
            builder.HasCipherFolderAssignment = true;
        }
        builder.AddStep(_ => new GenerateCiphersStep(count, typeDist, pwDist, assignFolders));
        return builder;
    }

    /// <summary>
    /// Generate personal ciphers for each user, encrypted with their individual symmetric key.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="countPerUser">Number of personal ciphers per user</param>
    /// <param name="typeDist">Distribution of cipher types. Uses realistic defaults if null.</param>
    /// <param name="pwDist">Distribution of password strengths. Uses realistic defaults if null.</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddPersonalCiphers(
        this RecipeBuilder builder, int countPerUser,
        Distribution<CipherType>? typeDist = null,
        Distribution<PasswordStrength>? pwDist = null)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Personal ciphers require users. Call UseRoster() or AddUsers() first.");
        }

        builder.HasPersonalCiphers = true;
        builder.AddStep(_ => new GeneratePersonalCiphersStep(countPerUser, typeDist, pwDist));
        return builder;
    }

    /// <summary>
    /// Validates the builder state to ensure all required steps are present and dependencies are met.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when required steps missing or dependencies violated</exception>
    public static RecipeBuilder Validate(this RecipeBuilder builder)
    {
        if (!builder.HasOrg)
        {
            throw new InvalidOperationException(
                "Organization is required. Call UseOrganization() or CreateOrganization().");
        }

        if (!builder.HasOwner && !builder.HasRosterOwner)
        {
            throw new InvalidOperationException(
                "Owner is required. Call AddOwner() or declare a user with role 'owner' in the roster.");
        }

        if (builder.HasGeneratedCiphers && !builder.HasGenerator)
        {
            throw new InvalidOperationException(
                "Generated ciphers require a generator. Call WithGenerator() first.");
        }

        if (builder.HasPersonalCiphers && !builder.HasGenerator)
        {
            throw new InvalidOperationException(
                "Personal ciphers require a generator. Call WithGenerator() first.");
        }

        if (builder.HasFolders && !builder.HasGenerator)
        {
            throw new InvalidOperationException(
                "Folders require a generator. Call WithGenerator() first.");
        }

        if (builder.HasCipherFolderAssignment && !builder.HasFolders)
        {
            throw new InvalidOperationException(
                "Cipher folder assignment requires folders. Set 'folders: true' or call AddFolders() first.");
        }

        return builder;
    }
}

using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Registers collection steps on a <see cref="RecipeBuilder"/>: generated, structure-based and shaped shared
/// collections, and default user collections.
/// </summary>
public static class RecipeBuilderCollectionExtensions
{
    /// <summary>
    /// Generate collections with random assignments.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of collections to generate</param>
    /// <param name="density">Optional density profile for collection fan-out and permission control</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddCollections(this RecipeBuilder builder, int count, DensityProfile? density = null)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => CreateCollectionsStep.FromCount(count, density));
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
    /// Adds shared collections with long-tailed target sizes and shaped group and member grants, built from the
    /// access shape. Replaces <see cref="AddCollections(RecipeBuilder, int, DensityProfile?)"/>.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of collections to create</param>
    /// <param name="shape">The preset's access shape</param>
    /// <param name="permissions">Permission mix applied to group and direct grants</param>
    /// <param name="seed">Random seed for the shape</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    internal static RecipeBuilder AddShapedCollections(
        this RecipeBuilder builder,
        int count,
        SeedPresetAccessShape shape,
        Distribution<PermissionWeight> permissions,
        int seed)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Collections require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => new CreateShapedCollectionsStep(count, shape, permissions, seed));
        return builder;
    }

    /// <summary>
    /// Create one default user collection per Confirmed User or Custom member, filled with migrated items.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="options">Default collection settings from the preset</param>
    /// <param name="policyEnabledDate">When the Organization Data Ownership policy was enabled; the collections' dates</param>
    /// <param name="seed">Seed for the per-member item counts, member choice and dates</param>
    /// <param name="typeDist">Distribution of cipher types. Uses realistic defaults if null.</param>
    /// <param name="personalDist">The personal cipher distribution given to AddPersonalCiphers(); members it gives items keep an empty collection</param>
    /// <param name="personalCountPerUser">The flat personal cipher count given to AddPersonalCiphers(); 0 when none</param>
    /// <param name="sizing">How the non-largest sizes come from the size histogram</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when WithGenerator() was not called</exception>
    internal static RecipeBuilder AddDefaultCollections(
        this RecipeBuilder builder,
        SeedPresetDefaultCollections options,
        DateTime policyEnabledDate,
        int seed,
        Distribution<CipherType>? typeDist = null,
        Distribution<(int Min, int Max)>? personalDist = null,
        int personalCountPerUser = 0,
        DefaultCollectionSizing sizing = DefaultCollectionSizing.Sampled)
    {
        if (!builder.HasGenerator)
        {
            throw new InvalidOperationException(
                "Default collections require a generator. Call WithGenerator() first.");
        }

        builder.AddStep(_ => new CreateDefaultCollectionsStep(options, policyEnabledDate, seed, typeDist, personalDist, personalCountPerUser, sizing));
        return builder;
    }
}

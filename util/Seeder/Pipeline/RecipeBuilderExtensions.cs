using Bit.Seeder.Data.Enums;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Registers the seeded random generator on a <see cref="RecipeBuilder"/> and validates that the registered steps
/// meet their dependencies.
/// </summary>
public static class RecipeBuilderExtensions
{
    /// <summary>
    /// Initialize seeded random generator for reproducible test data.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="domain">Organization domain (used for seeding randomness)</param>
    /// <param name="seed">Optional explicit seed. If null, domain hash is used.</param>
    /// <param name="region">Optional region for generated names, card brands, and identity formats. If null, <see cref="GeographicRegion.Global"/> is used.</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder WithGenerator(this RecipeBuilder builder, string domain, int? seed = null, GeographicRegion? region = null)
    {
        builder.HasGenerator = true;
        builder.AddStep(_ => InitGeneratorStep.FromDomain(domain, seed, region));
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
        if (!builder.HasOrg && !builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Organization or individual user is required. Call UseOrganization(), CreateOrganization(), or CreateIndividualUser().");
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

        if (builder.HasCipherFolderAssignment && !builder.HasFolders && !builder.HasNamedFolders)
        {
            throw new InvalidOperationException(
                "Cipher folder assignment requires folders. Set 'folders: true' or call AddFolders() first.");
        }

        if (builder.HasBilling && !builder.HasOrg)
        {
            throw new InvalidOperationException(
                "Stripe billing requires an organization. Call UseOrganization() or CreateOrganization() first.");
        }

        return builder;
    }
}

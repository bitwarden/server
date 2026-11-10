using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Steps;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Registers cipher and folder steps on a <see cref="RecipeBuilder"/>: fixture and generated ciphers, attachments,
/// folders, and the preset's cipher-to-collection, folder and favorite assignments.
/// </summary>
public static class RecipeBuilderCipherExtensions
{
    /// <summary>
    /// Generate folders for each user using a configurable distribution.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="density">Optional density profile for folder count distribution override</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddFolders(this RecipeBuilder builder, DensityProfile? density = null)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers && !builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Folders require users. Call UseRoster() or AddUsers() first.");
        }

        builder.HasFolders = true;
        builder.AddStep(_ => new GenerateFoldersStep(density));
        return builder;
    }

    /// <summary>
    /// Create folders with explicit names for the current user.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="folderNames">List of folder display names (must be unique, case-insensitive)</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddNamedFolders(this RecipeBuilder builder, List<string> folderNames)
    {
        if (!builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Named folders require an individual user. Call CreateIndividualUser() first.");
        }

        builder.HasNamedFolders = true;
        builder.AddStep(_ => new CreateNamedFoldersStep(folderNames));
        return builder;
    }

    /// <summary>
    /// Use ciphers from embedded fixtures.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Cipher fixture name without extension</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when AddCiphers() was already called</exception>
    public static RecipeBuilder UseCiphers(this RecipeBuilder builder, string fixture, bool skipCollectionAssignment = false)
    {
        if (builder.HasGeneratedCiphers)
        {
            throw new InvalidOperationException(
                "Cannot call UseCiphers() after AddCiphers(). Choose one cipher source.");
        }

        builder.HasFixtureCiphers = true;
        builder.AddStep(_ => CreateCiphersStep.ForOrganization(fixture, skipCollectionAssignment));
        return builder;
    }

    /// <summary>
    /// Use ciphers from embedded fixtures, encrypted with the individual user's symmetric key.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Cipher fixture name without extension</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when AddCiphers() was already called</exception>
    public static RecipeBuilder UsePersonalVaultCiphers(this RecipeBuilder builder, string fixture)
    {
        if (builder.HasGeneratedCiphers)
        {
            throw new InvalidOperationException(
                "Cannot call UsePersonalVaultCiphers() after AddCiphers(). Choose one cipher source.");
        }

        builder.HasFixtureCiphers = true;
        builder.AddStep(_ => CreateCiphersStep.ForPersonalVault(fixture));
        return builder;
    }

    /// <summary>
    /// Create attachments for fixture ciphers that declare an <c>attachments</c> array, each in a
    /// specified historical encryption mode. A no-op when the fixture declares no attachments.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="fixture">Cipher fixture name without extension (the same fixture the ciphers came from)</param>
    /// <param name="personal">True for a personal vault (encrypt with the user key); false for an organization vault (org key)</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no fixture ciphers exist</exception>
    internal static RecipeBuilder UseCipherAttachments(this RecipeBuilder builder, string fixture, bool personal)
    {
        if (!builder.HasFixtureCiphers)
        {
            throw new InvalidOperationException(
                "Cipher attachments require fixture ciphers. Call UseCiphers() or UsePersonalVaultCiphers() first.");
        }

        builder.AddAsyncStep(_ => personal
            ? CreateCipherAttachmentsStep.ForPersonalVault(fixture)
            : CreateCipherAttachmentsStep.ForOrganization(fixture));
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
    /// <param name="density">Optional density profile for cipher-to-collection assignment control</param>
    /// <param name="repromptEveryNthCipher">When &gt; 0, sets Reprompt=Password on every Nth generated cipher. 0 disables.</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseCiphers() was already called</exception>
    public static RecipeBuilder AddCiphers(
        this RecipeBuilder builder,
        int count,
        Distribution<CipherType>? typeDist = null,
        Distribution<PasswordStrength>? pwDist = null,
        bool assignFolders = false,
        DensityProfile? density = null,
        int repromptEveryNthCipher = 0)
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
        builder.AddStep(_ => new GenerateCiphersStep(count, typeDist, pwDist, assignFolders, density, repromptEveryNthCipher));
        return builder;
    }

    /// <summary>
    /// Create collection assignments mapping specific ciphers to specific collections.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="assignments">Collection assignment tuples from the preset</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no roster users or fixture ciphers exist</exception>
    internal static RecipeBuilder CreateCipherCollections(this RecipeBuilder builder, List<SeedCollectionAssignment> assignments)
    {
        if (!builder.HasRosterUsers)
        {
            throw new InvalidOperationException(
                "Collection assignments require roster users. Call UseRoster() first.");
        }

        // HasFixtureCiphers is mutually exclusive with HasGeneratedCiphers (enforced by UseCiphers/AddCiphers)
        if (!builder.HasFixtureCiphers)
        {
            throw new InvalidOperationException(
                "Collection assignments require fixture ciphers. Call UseCiphers() first.");
        }

        builder.AddStep(_ => new CreateCipherCollectionsStep(assignments));
        return builder;
    }

    /// <summary>
    /// Create folder assignments mapping specific ciphers to specific folders for specific users.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="assignments">Folder assignment tuples from the preset</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no roster users or fixture ciphers exist</exception>
    internal static RecipeBuilder CreateCipherFolders(this RecipeBuilder builder, List<SeedFolderAssignment> assignments)
    {
        if (!builder.HasRosterUsers && !builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Folder assignments require users. Call UseRoster() or CreateIndividualUser() first.");
        }

        // HasFixtureCiphers is mutually exclusive with HasGeneratedCiphers (enforced by UseCiphers/AddCiphers)
        if (!builder.HasFixtureCiphers)
        {
            throw new InvalidOperationException(
                "Folder assignments require fixture ciphers. Call UseCiphers() first.");
        }

        builder.AddStep(_ => new CreateCipherFoldersStep(assignments));
        return builder;
    }

    /// <summary>
    /// Create favorite assignments marking specific ciphers as favorites for specific users.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="assignments">Favorite assignment tuples from the preset</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no roster users or fixture ciphers exist</exception>
    internal static RecipeBuilder CreateCipherFavorites(this RecipeBuilder builder, List<SeedFavoriteAssignment> assignments)
    {
        if (!builder.HasRosterUsers && !builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Favorite assignments require users. Call UseRoster() or CreateIndividualUser() first.");
        }

        // HasFixtureCiphers is mutually exclusive with HasGeneratedCiphers (enforced by UseCiphers/AddCiphers)
        if (!builder.HasFixtureCiphers)
        {
            throw new InvalidOperationException(
                "Favorite assignments require fixture ciphers. Call UseCiphers() first.");
        }

        builder.AddStep(_ => new CreateCipherFavoritesStep(assignments));
        return builder;
    }

    /// <summary>
    /// Generate personal ciphers for each user, encrypted with their individual symmetric key.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="countPerUser">Number of personal ciphers per user</param>
    /// <param name="typeDist">Distribution of cipher types. Uses realistic defaults if null.</param>
    /// <param name="pwDist">Distribution of password strengths. Uses realistic defaults if null.</param>
    /// <param name="density">Optional density profile for per-user personal cipher count distribution</param>
    /// <param name="repromptEveryNthCipher">When &gt; 0, sets Reprompt=Password on every Nth generated personal cipher. 0 disables.</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddPersonalCiphers(
        this RecipeBuilder builder, int countPerUser,
        Distribution<CipherType>? typeDist = null,
        Distribution<PasswordStrength>? pwDist = null,
        DensityProfile? density = null,
        int repromptEveryNthCipher = 0)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers && !builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Personal ciphers require users. Call UseRoster() or AddUsers() first.");
        }

        builder.HasPersonalCiphers = true;
        builder.AddStep(_ => new GeneratePersonalCiphersStep(countPerUser, typeDist, pwDist, density, repromptEveryNthCipher));
        return builder;
    }
}

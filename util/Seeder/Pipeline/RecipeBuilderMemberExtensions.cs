using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Registers user and group steps on a <see cref="RecipeBuilder"/>: the individual user, roster or generated members,
/// generated and shaped groups, and the individual user's Stripe premium billing.
/// </summary>
public static class RecipeBuilderMemberExtensions
{
    /// <summary>
    /// Create a standalone individual user with no organization affiliation.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="email">User email address (domain is extracted for context)</param>
    /// <param name="premium">Whether the account has premium status</param>
    /// <param name="maxStorageGb">Optional max storage override in GB</param>
    /// <param name="selfHosted">When true, writes a license file after user creation (required for self-hosted premium validation)</param>
    /// <param name="creationDate">Optional backdated CreationDate for aged-account scenarios. Null uses the current time.</param>
    /// <returns>The builder for fluent chaining</returns>
    public static RecipeBuilder CreateIndividualUser(
        this RecipeBuilder builder, string email, bool premium, short maxStorageGb, bool selfHosted = false,
        DateTime? creationDate = null)
    {
        builder.HasIndividualUser = true;
        builder.HasOwner = true;
        builder.AddStep(_ => new CreateIndividualUserStep(email, premium, maxStorageGb, true, creationDate));
        if (selfHosted)
        {
            builder.AddAsyncStep(sp => new GenerateSelfHostUserLicenseStep(
                sp.GetRequiredService<ILicensingService>,
                sp.GetRequiredService<ISeederLicenseSigner>(),
                sp.GetRequiredService<ILogger<GenerateSelfHostUserLicenseStep>>()));
        }
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
    /// <param name="statusMix">Explicit status distribution; takes precedence over <paramref name="realisticStatusMix"/></param>
    /// <param name="adminCount">Number of Confirmed members created as Admins (the rest are Users)</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when UseRoster() was already called</exception>
    public static RecipeBuilder AddUsers(
        this RecipeBuilder builder,
        int count,
        bool realisticStatusMix = false,
        Distribution<OrganizationUserStatusType>? statusMix = null,
        int adminCount = 0)
    {
        if (builder.HasRosterUsers)
        {
            throw new InvalidOperationException(
                "Cannot call AddUsers() after UseRoster(). Choose one user source.");
        }

        builder.HasGeneratedUsers = true;
        builder.AddStep(_ => new CreateUsersStep(count, realisticStatusMix, statusMix, adminCount));
        return builder;
    }

    /// <summary>
    /// Generate groups with random members from existing users.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of groups to generate</param>
    /// <param name="density">Optional density profile for membership distribution control</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    public static RecipeBuilder AddGroups(this RecipeBuilder builder, int count, DensityProfile? density = null)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Groups require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => new CreateGroupsStep(count, density));
        return builder;
    }

    /// <summary>
    /// Adds groups with overlapping, long-tailed membership (access shape). Replaces <see cref="AddGroups"/>.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="count">Number of groups to generate, including the everyone and hot groups</param>
    /// <param name="shape">The preset's access shape; its <c>groups</c> block drives membership</param>
    /// <param name="seed">Random seed for the group shape</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no users exist</exception>
    internal static RecipeBuilder AddShapedGroups(this RecipeBuilder builder, int count, SeedPresetAccessShape shape, int seed)
    {
        if (!builder.HasRosterUsers && !builder.HasGeneratedUsers)
        {
            throw new InvalidOperationException(
                "Groups require users. Call UseRoster() or AddUsers() first.");
        }

        builder.AddStep(_ => new CreateShapedGroupsStep(count, shape, seed));
        return builder;
    }

    /// <summary>
    /// Create a real Stripe test-environment customer and premium subscription for the individual user.
    /// </summary>
    /// <param name="builder">The recipe builder</param>
    /// <param name="profile">The test card and billing address the user pays with</param>
    /// <returns>The builder for fluent chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when no individual user exists</exception>
    /// <remarks>
    /// Opt-in only. This step makes live network calls. Register it last, after every other pre-commit step,
    /// so Stripe is called only once the rest of the seed has succeeded.
    /// </remarks>
    public static RecipeBuilder WithStripeUserBilling(this RecipeBuilder builder, StripeBillingProfile profile)
    {
        if (!builder.HasIndividualUser)
        {
            throw new InvalidOperationException(
                "Stripe user billing requires an individual user. Call CreateIndividualUser() first.");
        }

        builder.AddAsyncStep(sp => new CreatePremiumSubscriptionStep(
            sp.GetRequiredService<IUserStripeBillingService>(), profile));
        return builder;
    }
}

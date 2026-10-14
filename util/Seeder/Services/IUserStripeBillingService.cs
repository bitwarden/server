using Bit.Core.Entities;
using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Services;

/// <summary>
/// Creates real Stripe test-environment billing (customer + subscription) for a seeded individual user.
/// </summary>
/// <remarks>
/// An interface for the same reasons as <see cref="IOrganizationStripeBillingService"/>: a mockable seam for
/// tests, and an optional capability a host can leave null on <c>SeederDependencies.UserStripeBillingService</c>.
/// </remarks>
public interface IUserStripeBillingService
{
    /// <summary>
    /// Fails fast when the host is not configured for Stripe test-mode billing. Call this
    /// <strong>before</strong> creating any entity so an opt-in with bad configuration cannot leave
    /// half-seeded data behind.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is unusable.</exception>
    void ValidateConfiguration();

    /// <summary>
    /// Creates the Stripe customer and an active <paramref name="subscriptionType"/> subscription for
    /// <paramref name="user"/>, then sets the user's subscription and gateway fields. Writes nothing to the
    /// database.
    /// </summary>
    /// <remarks>
    /// Must run before the user row is committed, so the bulk commit writes those fields with the rest of the
    /// row. Calls <see cref="ValidateConfiguration"/> itself before contacting Stripe, and leaves the user
    /// unchanged unless both Stripe calls succeed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="subscriptionType"/> has no Stripe billing mapping.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Stripe billing fails. The message names any Stripe customer already created.
    /// </exception>
    Task CreateSubscriptionAsync(User user, UserSubscriptionType subscriptionType);
}

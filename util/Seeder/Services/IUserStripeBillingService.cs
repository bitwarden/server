using Bit.Core.Entities;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;

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
    /// Creates the Stripe customer and a <paramref name="subscriptionType"/> subscription for
    /// <paramref name="user"/>, paid with <paramref name="profile"/>'s card at its address, then sets the
    /// user's subscription and gateway fields. Writes nothing to the database.
    /// </summary>
    /// <remarks>
    /// Must run before the user row is committed, so the bulk commit writes those fields with the rest of the
    /// row. Calls <see cref="ValidateConfiguration"/> itself before contacting Stripe, and leaves the user
    /// unchanged unless both Stripe calls succeed. The subscription comes back <c>active</c>, or
    /// <c>incomplete</c> when the card's first charge fails; the user is premium either way, as in production.
    /// The profile's tax ID, if any, is not sent: a personal account takes none.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="subscriptionType"/> has no Stripe billing mapping.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Stripe billing fails. The message names any Stripe customer already created.
    /// </exception>
    Task CreateSubscriptionAsync(User user, UserSubscriptionType subscriptionType, StripeBillingProfile profile);
}

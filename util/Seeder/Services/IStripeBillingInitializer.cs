using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Seeder.Options;

namespace Bit.Seeder.Services;

/// <summary>
/// Creates real Stripe test-environment billing (customer + subscription) for a seeded organization.
/// </summary>
/// <remarks>
/// An interface rather than logic inlined into <see cref="Steps.FinalizeOrganizationBillingStep"/> for two
/// reasons: it gives that step and <see cref="Pipeline.RecipeOrchestrator"/>'s pre-flight validation a
/// mockable seam for tests, and it lets <c>SeederDependencies.BillingInitializer</c> stay an optional
/// capability — a host that doesn't want to compose the full Core billing DI graph can simply leave it null.
/// </remarks>
public interface IStripeBillingInitializer
{
    /// <summary>
    /// Fails fast when the host is not configured for Stripe test-mode billing, or when the requested plan
    /// cannot be billed. Call this <strong>before</strong> creating any entity so an opt-in with bad
    /// configuration cannot leave half-seeded data behind.
    /// </summary>
    /// <param name="planType">The plan the organization will be seeded on.</param>
    /// <exception cref="InvalidOperationException">Thrown when the configuration or plan is unusable.</exception>
    void ValidateConfiguration(PlanType planType);

    /// <summary>
    /// Creates the Stripe customer and subscription for <paramref name="organization"/> and writes the
    /// resulting gateway identifiers back to the database.
    /// </summary>
    /// <remarks>
    /// Must run after the organization row is committed: the underlying billing service persists its
    /// changes with <c>IOrganizationRepository.ReplaceAsync</c>. Calls <see cref="ValidateConfiguration"/>
    /// itself before doing anything else, so a caller that skips the pre-flight check still fails fast
    /// rather than reaching Stripe with bad configuration.
    /// </remarks>
    Task InitializeOrganizationAsync(Organization organization, StripeBillingOptions options);
}

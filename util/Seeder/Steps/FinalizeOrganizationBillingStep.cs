using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates the seeded organization's Stripe customer and subscription.
/// </summary>
/// <remarks>
/// Deferred until after the bulk commit (<see cref="IPostCommitStep"/>) because the billing services persist
/// their changes with <c>IOrganizationRepository.ReplaceAsync</c> — the organization row has to exist first.
/// Registered only by <c>RecipeBuilderExtensions.WithStripeBilling</c>, never appended automatically, so a
/// default seed makes no Stripe calls.
/// </remarks>
internal sealed class FinalizeOrganizationBillingStep(
    IStripeBillingInitializer initializer,
    StripeBillingOptions options) : IAsyncStep, IPostCommitStep
{
    public Task ExecuteAsync(SeederContext context) =>
        initializer.InitializeOrganizationAsync(context.RequireOrganization(), options);
}

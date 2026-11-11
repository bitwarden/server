using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates the seeded premium user's Stripe customer and subscription.
/// </summary>
/// <remarks>
/// Runs as the last pre-commit step, not as an <see cref="IPostCommitStep"/>, so <see cref="BulkCommitter"/>
/// writes the user row once with its billing fields, and Stripe is called only after every other pre-commit
/// step has succeeded. Registered only by <c>RecipeBuilderExtensions.WithStripeUserBilling</c>, never
/// appended automatically, so a default seed makes no Stripe calls.
/// </remarks>
internal sealed class CreatePremiumSubscriptionStep(
    IUserStripeBillingService billingService,
    StripeBillingProfile profile) : IAsyncStep
{
    public Task ExecuteAsync(SeederContext context) =>
        billingService.CreateSubscriptionAsync(context.RequireOwner(), UserSubscriptionType.Premium, profile);
}

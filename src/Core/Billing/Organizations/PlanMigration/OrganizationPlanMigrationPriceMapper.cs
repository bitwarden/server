using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.PlanMigration;

internal static class OrganizationPlanMigrationPriceMapper
{
    /// <summary>
    /// Returns the target plan's equivalent price ID, or null if no mapping exists.
    /// </summary>
    public static string? MapOrNull(string sourcePriceId, Plan source, Plan target) =>
        Resolve(sourcePriceId, source, target);

    /// <summary>
    /// Maps as <see cref="MapOrNull"/>; returns the input unchanged on miss. Short-circuits when
    /// source and target are the same instance. Pass-through is intentional for Families and
    /// uniform-price slots — callers should not log misses.
    /// </summary>
    public static string MapOrPassThrough(string sourcePriceId, Plan source, Plan target)
    {
        if (ReferenceEquals(source, target))
        {
            return sourcePriceId;
        }
        return Resolve(sourcePriceId, source, target) ?? sourcePriceId;
    }

    private static string? Resolve(string sourcePriceId, Plan source, Plan target) => sourcePriceId switch
    {
        // Packaged -> Scalable PM base price (Teams Starter -> Teams Current): a packaged source holds its
        // flat price in StripePlanId, mapped to the target's per-seat price. The IsNullOrEmpty guard keeps a
        // null == null match from mis-mapping Scalable sources, whose StripePlanId is null.
        _ when !string.IsNullOrEmpty(source.PasswordManager.StripePlanId) &&
            sourcePriceId == source.PasswordManager.StripePlanId =>
            target.PasswordManager.StripeSeatPlanId,
        _ when sourcePriceId == source.PasswordManager.StripeSeatPlanId =>
            target.PasswordManager.StripeSeatPlanId,
        _ when sourcePriceId == source.PasswordManager.StripeStoragePlanId =>
            target.PasswordManager.StripeStoragePlanId,
        _ when source.SecretsManager is not null && target.SecretsManager is not null &&
            sourcePriceId == source.SecretsManager.StripeSeatPlanId =>
            target.SecretsManager.StripeSeatPlanId,
        _ when source.SecretsManager is not null && target.SecretsManager is not null &&
            sourcePriceId == source.SecretsManager.StripeServiceAccountPlanId =>
            target.SecretsManager.StripeServiceAccountPlanId,
        _ => null
    };
}

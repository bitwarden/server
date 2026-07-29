using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.PlanMigration;

internal static class OrganizationPlanMigrationPriceMapper
{
    /// <summary>
    /// Returns the target plan's equivalent price ID, or null if no mapping exists.
    /// </summary>
    public static string? MapOrNull(string sourcePriceId, Plan source, Plan target)
    {
        var slot = ResolveSlot(sourcePriceId, source);
        return slot is null ? null : TargetPriceId(slot.Value, source, target);
    }

    /// <summary>
    /// Maps as <see cref="MapOrNull"/>; returns the input unchanged on miss. Short-circuits when
    /// source and target are the same instance. Pass-through is intentional for Families and
    /// uniform-price slots, so callers should not log misses.
    /// </summary>
    public static string MapOrPassThrough(string sourcePriceId, Plan source, Plan target)
    {
        if (ReferenceEquals(source, target))
        {
            return sourcePriceId;
        }
        return MapOrNull(sourcePriceId, source, target) ?? sourcePriceId;
    }

    private static PlanPriceSlot? ResolveSlot(string sourcePriceId, Plan source) => sourcePriceId switch
    {
        // The IsNullOrEmpty guard keeps a null == null match from mis-mapping Scalable sources,
        // whose StripePlanId is null.
        _ when !string.IsNullOrEmpty(source.PasswordManager.StripePlanId) &&
            sourcePriceId == source.PasswordManager.StripePlanId =>
            PlanPriceSlot.PasswordManagerPackagedBase,
        _ when sourcePriceId == source.PasswordManager.StripeSeatPlanId =>
            PlanPriceSlot.PasswordManagerSeat,
        _ when sourcePriceId == source.PasswordManager.StripeStoragePlanId =>
            PlanPriceSlot.PasswordManagerStorage,
        _ when source.SecretsManager is not null &&
            sourcePriceId == source.SecretsManager.StripeSeatPlanId =>
            PlanPriceSlot.SecretsManagerSeat,
        _ when source.SecretsManager is not null &&
            sourcePriceId == source.SecretsManager.StripeServiceAccountPlanId =>
            PlanPriceSlot.SecretsManagerServiceAccount,
        _ => null
    };

    private static string? TargetPriceId(PlanPriceSlot slot, Plan source, Plan target) => slot switch
    {
        // A packaged base maps onto the target's per-seat price.
        PlanPriceSlot.PasswordManagerPackagedBase => target.PasswordManager.StripeSeatPlanId,
        PlanPriceSlot.PasswordManagerSeat => target.PasswordManager.StripeSeatPlanId,
        PlanPriceSlot.PasswordManagerStorage => target.PasswordManager.StripeStoragePlanId,
        PlanPriceSlot.SecretsManagerSeat when source.SecretsManager is not null && target.SecretsManager is not null =>
            target.SecretsManager.StripeSeatPlanId,
        PlanPriceSlot.SecretsManagerServiceAccount when source.SecretsManager is not null && target.SecretsManager is not null =>
            target.SecretsManager.StripeServiceAccountPlanId,
        _ => null
    };
}

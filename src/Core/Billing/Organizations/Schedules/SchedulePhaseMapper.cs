using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.PlanMigration;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.Schedules;

/// <summary>
/// Shared helpers for rebuilding subscription-schedule phase items, used by both the annual-upgrade
/// and price-migration edit paths.
/// </summary>
internal static class SchedulePhaseMapper
{
    /// <summary>
    /// True when any of the phase's items are priced on the target plan, which marks it as the
    /// post-transition phase.
    /// </summary>
    public static bool PhaseUsesTargetPlanPrices(SubscriptionSchedulePhase phase, Plan target)
    {
        var targetIds = new HashSet<string>(StringComparer.Ordinal)
        {
            target.PasswordManager.StripeSeatPlanId,
            target.PasswordManager.StripeStoragePlanId
        };
        if (target.SecretsManager?.StripeSeatPlanId is { } smSeat)
        {
            targetIds.Add(smSeat);
        }
        if (target.SecretsManager?.StripeServiceAccountPlanId is { } smServiceAccount)
        {
            targetIds.Add(smServiceAccount);
        }

        return phase.Items.Any(item => targetIds.Contains(item.PriceId));
    }

    /// <summary>
    /// Projects a phase's items into options, copying item-level discounts by coupon, then applies the
    /// subscription changes with source-to-target price translation.
    /// </summary>
    public static List<SubscriptionSchedulePhaseItemOptions> ApplyChangesToPhaseItems(
        IList<SubscriptionSchedulePhaseItem> phaseItems,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan sourcePlan,
        Plan targetPlan)
    {
        string Translate(string priceId) =>
            OrganizationPlanMigrationPriceMapper.MapOrPassThrough(priceId, sourcePlan, targetPlan);

        var items = phaseItems
            .Select(i => new SubscriptionSchedulePhaseItemOptions
            {
                Price = i.PriceId,
                Quantity = i.Quantity,
                Discounts = DiscountExtensions.BuildPhaseItemLevelDiscounts(i.Discounts?.Select(d => d.CouponId) ?? [])
            })
            .ToList();

        foreach (var change in changes)
        {
            change.Switch(
                addItem => items.Add(new SubscriptionSchedulePhaseItemOptions
                {
                    Price = Translate(addItem.PriceId),
                    Quantity = addItem.Quantity
                }),
                changeItemPrice =>
                {
                    var translatedCurrent = Translate(changeItemPrice.CurrentPriceId);
                    var translatedUpdated = Translate(changeItemPrice.UpdatedPriceId);
                    var existing = items.FirstOrDefault(i => i.Price == translatedCurrent);
                    if (existing != null)
                    {
                        existing.Price = translatedUpdated;
                        if (changeItemPrice.Quantity.HasValue)
                        {
                            existing.Quantity = changeItemPrice.Quantity.Value;
                        }
                    }
                },
                removeItem =>
                {
                    var translated = Translate(removeItem.PriceId);
                    items.RemoveAll(i => i.Price == translated);
                },
                updateItemQuantity =>
                {
                    var translated = Translate(updateItemQuantity.PriceId);
                    if (updateItemQuantity.Quantity == 0)
                    {
                        items.RemoveAll(i => i.Price == translated);
                    }
                    else
                    {
                        var existing = items.FirstOrDefault(i => i.Price == translated);
                        if (existing != null)
                        {
                            existing.Quantity = updateItemQuantity.Quantity;
                        }
                        else
                        {
                            items.Add(new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = translated,
                                Quantity = updateItemQuantity.Quantity
                            });
                        }
                    }
                });
        }

        return items;
    }
}

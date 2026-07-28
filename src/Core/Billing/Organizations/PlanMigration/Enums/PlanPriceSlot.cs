namespace Bit.Core.Billing.Organizations.PlanMigration.Enums;

/// <summary>
/// Identifies which priced slot of a <see cref="Bit.Core.Models.StaticStore.Plan"/> a Stripe price
/// ID occupies. Resolving the slot once lets the price mapper derive both the target price ID and
/// the catalog unit prices from a single switch, so the savings quote and the schedule it
/// describes can never disagree about what a line item is.
/// </summary>
internal enum PlanPriceSlot
{
    /// <summary>
    /// A Packaged plan's flat base price. Mappable to a target seat price for schedule
    /// construction, but it has no per-unit price to quote a comparison from.
    /// </summary>
    PasswordManagerPackagedBase,
    PasswordManagerSeat,
    PasswordManagerStorage,
    SecretsManagerSeat,
    SecretsManagerServiceAccount
}

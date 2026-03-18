using Bit.Core.Models.StaticStore;
using OneOf;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// Adds a new line item to the subscription.
/// </summary>
public record AddItem(string PriceId, int Quantity);

/// <summary>
/// Replaces an existing line item's price (e.g. upgrading from Teams to Enterprise).
/// Optionally updates the quantity; if <c>null</c>, the current quantity is preserved.
/// </summary>
public record ChangeItemPrice(string CurrentPriceId, string UpdatedPriceId, int? Quantity);

/// <summary>
/// Removes a line item from the subscription.
/// </summary>
public record RemoveItem(string PriceId);

/// <summary>
/// Updates the quantity of an existing line item. Setting quantity to 0 deletes the item.
/// </summary>
public record UpdateItemQuantity(string PriceId, int Quantity);

/// <summary>
/// A union type representing a single change to apply to an organization's Stripe subscription.
/// </summary>
public class OrganizationSubscriptionChange(OneOf<AddItem, ChangeItemPrice, RemoveItem, UpdateItemQuantity> input)
    : OneOfBase<AddItem, ChangeItemPrice, RemoveItem, UpdateItemQuantity>(input)
{
    public static implicit operator OrganizationSubscriptionChange(AddItem addItem) =>
        new(addItem);

    public static implicit operator OrganizationSubscriptionChange(ChangeItemPrice changeItemPrice) =>
        new(changeItemPrice);

    public static implicit operator OrganizationSubscriptionChange(RemoveItem removeItem) =>
        new(removeItem);

    public static implicit operator OrganizationSubscriptionChange(UpdateItemQuantity updateItemQuantity) =>
        new(updateItemQuantity);
}

/// <summary>
/// A collection of <see cref="OrganizationSubscriptionChange"/> items to apply atomically to
/// an organization's Stripe subscription. Use <see cref="Builder"/> to compose changes using
/// domain-specific methods that encapsulate the correct Stripe price IDs and proration behavior.
/// </summary>
public record OrganizationSubscriptionChangeSet
{
    public required IReadOnlyList<OrganizationSubscriptionChange> Changes { get; init; } = [];

    /// <summary>
    /// When <c>true</c>, all changes in this set use <c>ProrationBehavior.AlwaysInvoice</c>
    /// to bill immediately. Set automatically by the builder for structural operations
    /// (plan upgrades, sponsorship swaps). When <c>false</c> (the default), changes are prorated.
    /// </summary>
    public bool ChargeImmediately { get; init; }

    public static OrganizationSubscriptionChangeSetBuilder Builder(Plan plan) => new(plan);
}

/// <summary>
/// Builds an <see cref="OrganizationSubscriptionChangeSet"/> using domain-specific methods that
/// encapsulate the correct Stripe price IDs and proration behavior. Structural operations
/// (plan upgrades, sponsorship swaps) automatically set
/// <see cref="OrganizationSubscriptionChangeSet.ChargeImmediately"/> so that they are billed
/// immediately. All other operations (add-ons, seat updates) default to proration.
/// </summary>
public class OrganizationSubscriptionChangeSetBuilder(Plan currentPlan)
{
    private readonly List<OrganizationSubscriptionChange> _changes = [];
    private bool _chargeImmediately;

    // Add-on operations (prorated by default)

    /// <summary>
    /// Adds a new storage line item to the subscription. Use when the organization has no
    /// existing additional storage (first-time add).
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder AddStorage(int quantity)
    {
        _changes.Add(new AddItem(currentPlan.PasswordManager.StripeStoragePlanId, quantity));
        return this;
    }

    /// <summary>
    /// Updates the quantity of an existing storage line item.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder UpdateStorage(int quantity)
    {
        _changes.Add(new UpdateItemQuantity(currentPlan.PasswordManager.StripeStoragePlanId, quantity));
        return this;
    }

    /// <summary>
    /// Adds a new SM service account line item to the subscription. Use when the organization
    /// has no existing additional service accounts (first-time add).
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder AddServiceAccounts(int quantity)
    {
        _changes.Add(new AddItem(currentPlan.SecretsManager.StripeServiceAccountPlanId, quantity));
        return this;
    }

    /// <summary>
    /// Updates the quantity of an existing SM service account line item.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder UpdateServiceAccounts(int quantity)
    {
        _changes.Add(new UpdateItemQuantity(currentPlan.SecretsManager.StripeServiceAccountPlanId, quantity));
        return this;
    }

    // Seat operations

    /// <summary>
    /// Updates the quantity of the Password Manager seat line item.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder UpdatePasswordManagerSeats(int quantity)
    {
        _changes.Add(new UpdateItemQuantity(currentPlan.PasswordManager.StripeSeatPlanId, quantity));
        return this;
    }

    /// <summary>
    /// Updates the quantity of the Secrets Manager seat line item.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder UpdateSecretsManagerSeats(int quantity)
    {
        _changes.Add(new UpdateItemQuantity(currentPlan.SecretsManager.StripeSeatPlanId, quantity));
        return this;
    }

    // Structural operations (charge immediately)

    /// <summary>
    /// Replaces the current plan's base line item with a sponsored plan. Removes the current
    /// plan's line item and adds the sponsored plan's line item with quantity 1.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder EstablishSponsorship(SponsoredPlan sponsoredPlan)
    {
        _changes.Add(new RemoveItem(currentPlan.PasswordManager.StripePlanId));
        _changes.Add(new AddItem(sponsoredPlan.StripePlanId, 1));
        _chargeImmediately = true;
        return this;
    }

    /// <summary>
    /// Swaps the Password Manager plan price from the current plan to <paramref name="targetPlan"/>.
    /// Handles both seat-based and non-seat-based plans.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder ChangePasswordManagerPrice(Plan targetPlan)
    {
        _changes.Add(new ChangeItemPrice(
            GetPasswordManagerPriceId(currentPlan),
            GetPasswordManagerPriceId(targetPlan),
            null));
        _chargeImmediately = true;
        return this;
    }

    /// <summary>
    /// Swaps the storage price from the current plan to <paramref name="targetPlan"/>.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder ChangeStoragePrice(Plan targetPlan)
    {
        _changes.Add(new ChangeItemPrice(
            currentPlan.PasswordManager.StripeStoragePlanId,
            targetPlan.PasswordManager.StripeStoragePlanId,
            null));
        _chargeImmediately = true;
        return this;
    }

    /// <summary>
    /// Swaps the Secrets Manager seat price from the current plan to <paramref name="targetPlan"/>.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder ChangeSecretsManagerSeatPrice(Plan targetPlan)
    {
        _changes.Add(new ChangeItemPrice(
            currentPlan.SecretsManager.StripeSeatPlanId,
            targetPlan.SecretsManager.StripeSeatPlanId,
            null));
        _chargeImmediately = true;
        return this;
    }

    /// <summary>
    /// Swaps the SM service account price from the current plan to <paramref name="targetPlan"/>.
    /// </summary>
    public OrganizationSubscriptionChangeSetBuilder ChangeServiceAccountPrice(Plan targetPlan)
    {
        _changes.Add(new ChangeItemPrice(
            currentPlan.SecretsManager.StripeServiceAccountPlanId,
            targetPlan.SecretsManager.StripeServiceAccountPlanId,
            null));
        _chargeImmediately = true;
        return this;
    }

    public OrganizationSubscriptionChangeSet Build() =>
        new() { Changes = [.. _changes], ChargeImmediately = _chargeImmediately };

    private static string GetPasswordManagerPriceId(Plan p) =>
        p.HasNonSeatBasedPasswordManagerPlan()
            ? p.PasswordManager.StripePlanId
            : p.PasswordManager.StripeSeatPlanId;
}

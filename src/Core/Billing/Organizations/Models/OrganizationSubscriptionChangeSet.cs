using Bit.Core.Models.StaticStore;
using OneOf;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// Adds a new line item to the subscription.
/// </summary>
public record AddItem(string PriceId, int Quantity);

/// <summary>
/// Replaces an existing line item's price (e.g. switching from monthly to annual billing).
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
/// A change is considered "structural" (triggering immediate invoicing) if it adds, removes,
/// or re-prices a line item, or sets a quantity to 0. Non-structural quantity updates use prorations.
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

    public bool IsItemAddition => IsT0;
    public bool IsItemPriceChange => IsT1;
    public bool IsItemRemoval => IsT2;
    public bool IsItemQuantityUpdate => IsT3;
    public bool IsStructural => !IsItemQuantityUpdate || AsT3.Quantity == 0;
}

/// <summary>
/// A collection of <see cref="OrganizationSubscriptionChange"/> items to apply atomically to
/// an organization's Stripe subscription. Use the static factory methods for common single-change
/// operations, or <see cref="Builder"/> for composing multiple changes.
/// </summary>
public record OrganizationSubscriptionChangeSet
{
    public required IReadOnlyList<OrganizationSubscriptionChange> Changes { get; init; } = [];

    public static OrganizationSubscriptionChangeSet UpdatePasswordManagerSeats(Plan plan, int seats) =>
        new()
        {
            Changes =
            [
                new UpdateItemQuantity(plan.PasswordManager.StripeSeatPlanId, seats)
            ]
        };

    public static OrganizationSubscriptionChangeSet UpdateStorage(Plan plan, int storage) =>
        new()
        {
            Changes =
            [
                new UpdateItemQuantity(plan.PasswordManager.StripeStoragePlanId, storage)
            ]
        };

    public static OrganizationSubscriptionChangeSet UpdateSecretsManagerSeats(Plan plan, int seats) =>
        new()
        {
            Changes =
            [
                new UpdateItemQuantity(plan.SecretsManager.StripeSeatPlanId, seats)
            ]
        };

    public static OrganizationSubscriptionChangeSet UpdateSecretsManagerServiceAccounts(Plan plan, int serviceAccounts) =>
        new()
        {
            Changes =
            [
                new UpdateItemQuantity(plan.SecretsManager.StripeServiceAccountPlanId, serviceAccounts)
            ]
        };

    public static OrganizationSubscriptionChangeSetBuilder Builder() => new();
}

public class OrganizationSubscriptionChangeSetBuilder
{
    private readonly List<OrganizationSubscriptionChange> _changes = [];

    public OrganizationSubscriptionChangeSetBuilder AddItem(string priceId, int quantity)
    {
        _changes.Add(new AddItem(priceId, quantity));
        return this;
    }

    public OrganizationSubscriptionChangeSetBuilder ChangeItemPrice(string currentPriceId, string updatedPriceId, int? quantity = null)
    {
        _changes.Add(new ChangeItemPrice(currentPriceId, updatedPriceId, quantity));
        return this;
    }

    public OrganizationSubscriptionChangeSetBuilder RemoveItem(string priceId)
    {
        _changes.Add(new RemoveItem(priceId));
        return this;
    }

    public OrganizationSubscriptionChangeSetBuilder UpdateItemQuantity(string priceId, int quantity)
    {
        _changes.Add(new UpdateItemQuantity(priceId, quantity));
        return this;
    }

    public OrganizationSubscriptionChangeSet Build() =>
        new() { Changes = _changes.AsReadOnly() };
}

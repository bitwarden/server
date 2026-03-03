using Bit.Core.Models.StaticStore;
using OneOf;

namespace Bit.Core.Billing.Organizations.Models;

public record AddItem(string PriceId, int Quantity);
public record ChangeItemPrice(string CurrentPriceId, string UpdatedPriceId, int? Quantity);
public record RemoveItem(string PriceId);
public record UpdateItemQuantity(string PriceId, int Quantity);

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

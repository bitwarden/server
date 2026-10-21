using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class SubscriptionExtensionsTests
{
    [Fact]
    public void HasPrivilegedControlsSeats_NullItems_ReturnsFalse()
    {
        var subscription = new Subscription { Items = null };

        Assert.False(subscription.HasPrivilegedControlsSeats());
    }

    [Fact]
    public void HasPrivilegedControlsSeats_NoItems_ReturnsFalse()
    {
        var subscription = new Subscription { Items = new StripeList<SubscriptionItem> { Data = [] } };

        Assert.False(subscription.HasPrivilegedControlsSeats());
    }

    [Fact]
    public void HasPrivilegedControlsSeats_PrivilegedControlsSeatReference_ReturnsTrue()
    {
        var subscription = SubscriptionWithReferences(
            StripeConstants.PurchasableReferences.PasswordManagerSeat,
            StripeConstants.PurchasableReferences.PrivilegedControlsSeat);

        Assert.True(subscription.HasPrivilegedControlsSeats());
    }

    [Fact]
    public void HasPrivilegedControlsSeats_OtherReferencesOnly_ReturnsFalse()
    {
        var subscription = SubscriptionWithReferences(
            StripeConstants.PurchasableReferences.PasswordManagerSeat,
            StripeConstants.PurchasableReferences.SecretsManagerSeat);

        Assert.False(subscription.HasPrivilegedControlsSeats());
    }

    [Fact]
    public void HasPrivilegedControlsSeats_PriceWithoutMetadata_ReturnsFalse()
    {
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_pam_seat", Metadata = null } }]
            }
        };

        Assert.False(subscription.HasPrivilegedControlsSeats());
    }

    private static Subscription SubscriptionWithReferences(params string[] references) => new()
    {
        Items = new StripeList<SubscriptionItem>
        {
            Data = references.Select(reference => new SubscriptionItem
            {
                Price = new Price
                {
                    Id = $"price_{reference}",
                    Metadata = new Dictionary<string, string>
                    {
                        [StripeConstants.MetadataKeys.PurchasableReference] = reference
                    }
                }
            }).ToList()
        }
    };
}

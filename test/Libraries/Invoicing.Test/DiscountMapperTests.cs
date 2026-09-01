using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class DiscountMapperTests
{
    private static Invoice Deserialize(string json) => Invoice.FromJson(json);

    [Fact]
    public void Partition_NoDiscounts_ReturnsEmptySets()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 12790,
          "lines": { "data": [] }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        Assert.Empty(result.CartLevel);
        Assert.Empty(result.ItemLevel);
    }

    [Fact]
    public void Partition_CartWideCoupon_LandsInCartLevel_WithAggregateAmount()
    {
        // no applies_to on the coupon -> cart-wide, not item-scoped.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_cart", "source": { "coupon": { "id": "cp_cart", "name": "WELCOME10", "percent_off": 10 } } } }
          ],
          "lines": { "data": [] }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        Assert.Equal(12.79m, Assert.Single(result.CartLevel).Amount);
        Assert.Empty(result.ItemLevel);
    }

    [Fact]
    public void Partition_ItemScopedCoupon_MatchesByDiscountId_WhenLineDiscountUnexpanded()
    {
        // line-level discount is an unexpanded id; the coupon is only on total_discount_amounts.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_x", "source": { "coupon": { "id": "cp_x", "name": "SEATS10", "percent_off": 10, "applies_to": { "products": ["prod_pm"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_x" } ]
              }
            ]
          }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        Assert.Empty(result.CartLevel);
        Assert.Equal(12.79m, Assert.Single(result.ItemLevel["pm-seat"]).Amount);
    }

    [Fact]
    public void Partition_MultiProductCoupon_AttachesUnderBothLines_WithoutDoubleCounting()
    {
        // one coupon scoped to two products; two lines each reference it via the same discount id.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 23964,
          "total_discount_amounts": [
            { "amount": 2558, "discount": { "id": "di_multi", "source": { "coupon": { "id": "cp_multi", "name": "BUNDLE10", "percent_off": 10, "applies_to": { "products": ["prod_pm", "prod_sm"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_multi" } ]
              },
              {
                "amount": 11982,
                "pricing": { "price_details": { "price": { "id": "price_sm", "metadata": { "purchasable_reference": "sm-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_multi" } ]
              }
            ]
          }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        Assert.Empty(result.CartLevel);
        Assert.Equal(12.79m, Assert.Single(result.ItemLevel["pm-seat"]).Amount);
        Assert.Equal(12.79m, Assert.Single(result.ItemLevel["sm-seat"]).Amount);
    }

    [Fact]
    public void Partition_PercentOffCoupon_MapsTypeAndValue()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_pct", "source": { "coupon": { "id": "cp_pct", "name": "TENOFF", "percent_off": 15.5 } } } }
          ],
          "lines": { "data": [] }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        var discount = Assert.Single(result.CartLevel);
        Assert.Equal(BitwardenDiscountType.PercentOff, discount.Type);
        Assert.Equal(15.5m, discount.Value);
    }

    [Fact]
    public void Partition_AmountOffCoupon_MapsValueInDollars()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_amt", "source": { "coupon": { "id": "cp_amt", "name": "FIVEOFF", "amount_off": 1598 } } } }
          ],
          "lines": { "data": [] }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        var discount = Assert.Single(result.CartLevel);
        Assert.Equal(BitwardenDiscountType.AmountOff, discount.Type);
        Assert.Equal(15.98m, discount.Value);
    }

    [Fact]
    public void Partition_CouponWithoutExpandedCoupon_LogsAndDrops()
    {
        // total_discount_amounts[].discount has no source.coupon -> can't resolve; must log and drop.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_unresolved" } }
          ],
          "lines": { "data": [] }
        }
        """);

        var logger = new RecordingLogger<DiscountMapperTests>();
        var result = DiscountMapper.Partition(invoice, logger);

        Assert.Empty(result.CartLevel);
        Assert.Empty(result.ItemLevel);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("di_unresolved", error);
    }

    [Fact]
    public void Partition_TotalDiscountWithoutDiscountId_LogsAndDrops()
    {
        // total_discount_amounts[].discount has a coupon but no id -> DiscountId is null; must log and drop, not throw.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "source": { "coupon": { "id": "cp_noid", "name": "NOID10", "percent_off": 10 } } } }
          ],
          "lines": { "data": [] }
        }
        """);

        var logger = new RecordingLogger<DiscountMapperTests>();
        var result = DiscountMapper.Partition(invoice, logger);

        Assert.Empty(result.CartLevel);
        Assert.Empty(result.ItemLevel);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public void Partition_ItemScopedCouponMatchingNoLine_LogsAndDrops()
    {
        // item-scoped coupon present in total_discount_amounts, but no line references it.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_orphan", "source": { "coupon": { "id": "cp_orphan", "name": "ORPHAN10", "percent_off": 10, "applies_to": { "products": ["prod_pm"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "discount_amounts": []
              }
            ]
          }
        }
        """);

        var logger = new RecordingLogger<DiscountMapperTests>();
        var result = DiscountMapper.Partition(invoice, logger);

        Assert.Empty(result.CartLevel);
        Assert.Empty(result.ItemLevel);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("di_orphan", error);
        Assert.Contains("ORPHAN10", error);
    }

    [Fact]
    public void Partition_ItemScopedDiscountOnUnknownReference_LogsAndDrops()
    {
        // line carries a non-empty but unknown purchasable_reference; its item-scoped discount must not attach.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_unknown", "source": { "coupon": { "id": "cp_u", "name": "MYSTERY10", "percent_off": 10, "applies_to": { "products": ["prod_x"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_x", "metadata": { "purchasable_reference": "provider-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_unknown" } ]
              }
            ]
          }
        }
        """);

        var logger = new RecordingLogger<DiscountMapperTests>();
        var result = DiscountMapper.Partition(invoice, logger);

        Assert.Empty(result.ItemLevel);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("di_unknown", error);
    }

    [Fact]
    public void Partition_ItemScopedDiscountOnProrationLine_DoesNotAttachToBase()
    {
        // the same item-scoped coupon appears on both the base line and a proration line for pm-seat.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1479, "discount": { "id": "di_seat", "source": { "coupon": { "id": "cp_seat", "name": "SEATS10", "percent_off": 10, "applies_to": { "products": ["prod_pm"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_seat" } ]
              },
              {
                "amount": 2000,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "parent": { "subscription_item_details": { "proration": true } },
                "discount_amounts": [ { "amount": 200, "discount": "di_seat" } ]
              }
            ]
          }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        // only the base line's 12.79 attaches; the proration line's 2.00 must not appear on the base item.
        var discount = Assert.Single(result.ItemLevel["pm-seat"]);
        Assert.Equal(12.79m, discount.Amount);
    }

    [Fact]
    public void Partition_ItemScopedCouponMatchingOnlyProrationLine_LogsAndDrops()
    {
        // the item-scoped coupon's only pm-seat line is a proration; the line loop skips it, so nothing attaches.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 200, "discount": { "id": "di_seat", "source": { "coupon": { "id": "cp_seat", "name": "SEATS10", "percent_off": 10, "applies_to": { "products": ["prod_pm"] } } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 2000,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "parent": { "subscription_item_details": { "proration": true } },
                "discount_amounts": [ { "amount": 200, "discount": "di_seat" } ]
              }
            ]
          }
        }
        """);

        var logger = new RecordingLogger<DiscountMapperTests>();
        var result = DiscountMapper.Partition(invoice, logger);

        Assert.Empty(result.CartLevel);
        Assert.Empty(result.ItemLevel);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("di_seat", error);
        Assert.Contains("SEATS10", error);
    }

    [Fact]
    public void Partition_LineCarriesCartWideDiscountId_StaysCartLevel_NotAttachedToItem()
    {
        // Stripe echoes a cart-wide coupon (no applies_to) onto the line's discount_amounts.
        // It must remain cart-level; the line loop must not pull it down to item-level.
        var invoice = Deserialize("""
        {
          "id": "in_test",
          "total": 11982,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_cart", "source": { "coupon": { "id": "cp_cart", "name": "WELCOME10", "percent_off": 10 } } } }
          ],
          "lines": {
            "data": [
              {
                "amount": 12790,
                "pricing": { "price_details": { "price": { "id": "price_pm", "metadata": { "purchasable_reference": "pm-seat" } } } },
                "discount_amounts": [ { "amount": 1279, "discount": "di_cart" } ]
              }
            ]
          }
        }
        """);

        var result = DiscountMapper.Partition(invoice, new RecordingLogger<DiscountMapperTests>());

        Assert.Equal(12.79m, Assert.Single(result.CartLevel).Amount);
        Assert.Empty(result.ItemLevel);
    }
}

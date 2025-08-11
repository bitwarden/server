using Bit.Core.Billing.Extensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class InvoiceExtensionsTests
{
    private static Invoice CreateInvoiceWithLines(params InvoiceLineItem[] lineItems)
    {
        return new Invoice
        {
            Lines = new StripeList<InvoiceLineItem>
            {
                Data = lineItems?.ToList() ?? new List<InvoiceLineItem>()
            }
        };
    }

    #region FormatForProvider Tests

    [Fact]
    public void FormatForProvider_NullLines_ReturnsEmptyList()
    {
        // Arrange
        var invoice = new Invoice
        {
            Lines = null
        };
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FormatForProvider_EmptyLines_ReturnsEmptyList()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines();
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FormatForProvider_NullLineItem_SkipsNullLine()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(null);
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FormatForProvider_LineWithNullDescription_SkipsLine()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem { Description = null, Quantity = 1, Amount = 1000 }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FormatForProvider_ProviderPortalTeams_FormatsCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams (at $6.00 / month)",
                Quantity = 5,
                Amount = 3000
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("5 × Manage service provider (at $6.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_ProviderPortalEnterprise_FormatsCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Enterprise (at $4.00 / month)",
                Quantity = 10,
                Amount = 4000
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("10 × Manage service provider (at $4.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_ProviderPortalWithoutPriceInfo_FormatsWithoutPrice()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams",
                Quantity = 3,
                Amount = 1800
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("3 × Manage service provider ", result[0]);
    }

    [Fact]
    public void FormatForProvider_BusinessUnitPortalEnterprise_FormatsCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Business Unit Portal - Enterprise (at $5.00 / month)",
                Quantity = 8,
                Amount = 4000
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("8 × Manage service provider (at $5.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_BusinessUnitPortalGeneric_FormatsCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Business Unit Portal (at $3.00 / month)",
                Quantity = 2,
                Amount = 600
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("2 × Manage service provider (at $3.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_TaxLineWithPriceInfo_FormatsCorrectly()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Tax (at $2.00 / month)",
                Quantity = 1,
                Amount = 200
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("1 × Tax (at $2.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_TaxLineWithoutPriceInfo_CalculatesPrice()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Tax",
                Quantity = 2,
                Amount = 400 // $4.00 total, $2.00 per item
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("2 × Tax (at $2.00 / month)", result[0]);
    }

    [Fact]
    public void FormatForProvider_TaxLineWithZeroQuantity_DoesNotCalculatePrice()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Tax",
                Quantity = 0,
                Amount = 200
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("0 × Tax ", result[0]);
    }

    [Fact]
    public void FormatForProvider_OtherLineItem_ReturnsAsIs()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Some other service",
                Quantity = 1,
                Amount = 1000
            }
        );
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("Some other service", result[0]);
    }

    [Fact]
    public void FormatForProvider_InvoiceLevelTax_AddsToResult()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams",
                Quantity = 1,
                Amount = 600
            }
        );
        invoice.Tax = 120; // $1.20 in cents
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("1 × Manage service provider ", result[0]);
        Assert.Equal("1 × Tax (at $1.20 / month)", result[1]);
    }

    [Fact]
    public void FormatForProvider_NoInvoiceLevelTax_DoesNotAddTax()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams",
                Quantity = 1,
                Amount = 600
            }
        );
        invoice.Tax = null;
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("1 × Manage service provider ", result[0]);
    }

    [Fact]
    public void FormatForProvider_ZeroInvoiceLevelTax_DoesNotAddTax()
    {
        // Arrange
        var invoice = CreateInvoiceWithLines(
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams",
                Quantity = 1,
                Amount = 600
            }
        );
        invoice.Tax = 0;
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Single(result);
        Assert.Equal("1 × Manage service provider ", result[0]);
    }

    [Fact]
    public void FormatForProvider_ComplexScenario_HandlesAllLineTypes()
    {
        // Arrange
        var lineItems = new StripeList<InvoiceLineItem>();
        lineItems.Data = new List<InvoiceLineItem>
        {
            new InvoiceLineItem
            {
                Description = "Provider Portal - Teams (at $6.00 / month)", Quantity = 5, Amount = 3000
            },
            new InvoiceLineItem
            {
                Description = "Provider Portal - Enterprise (at $4.00 / month)", Quantity = 10, Amount = 4000
            },
            new InvoiceLineItem { Description = "Tax", Quantity = 1, Amount = 800 },
            new InvoiceLineItem { Description = "Custom Service", Quantity = 2, Amount = 2000 }
        };

        var invoice = new Invoice
        {
            Lines = lineItems,
            Tax = 200 // Additional $2.00 tax
        };
        var subscription = new Subscription();

        // Act
        var result = invoice.FormatForProvider(subscription);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal("5 × Manage service provider (at $6.00 / month)", result[0]);
        Assert.Equal("10 × Manage service provider (at $4.00 / month)", result[1]);
        Assert.Equal("1 × Tax (at $8.00 / month)", result[2]);
        Assert.Equal("Custom Service", result[3]);
        Assert.Equal("1 × Tax (at $2.00 / month)", result[4]);
    }

    #endregion
}

using Bit.Invoicing.InvoicePreviews.Models;
using Xunit;

namespace Bit.Invoicing.Test.Models;

public class PasswordManagerInvoiceItemsTests
{
    private static readonly InvoicePreviewItem Seats = new() { Reference = "pm-seat", Quantity = 1, Cost = 10m };
    private static readonly PurchasableProration Proration = new() { Charge = 26.67m, Credit = 6.67m, Tax = 2m, Total = 20m, Months = 8 };

    [Fact]
    public void Constructor_WithSeatsOnly_Succeeds()
    {
        var items = new PasswordManagerInvoiceItems(seats: Seats);

        Assert.Same(Seats, items.Seats);
        Assert.Null(items.Prorations);
    }

    [Fact]
    public void Constructor_WithProrationOnly_Succeeds()
    {
        var items = new PasswordManagerInvoiceItems(prorations: [Proration]);

        Assert.Null(items.Seats);
        Assert.Same(Proration, Assert.Single(items.Prorations!));
    }

    [Fact]
    public void Constructor_WithSeatsAndProration_KeepsBoth()
    {
        var items = new PasswordManagerInvoiceItems(seats: Seats, prorations: [Proration]);

        Assert.Same(Seats, items.Seats);
        Assert.Single(items.Prorations!);
    }

    [Fact]
    public void Constructor_WithNeitherSeatsNorProration_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new PasswordManagerInvoiceItems(additionalStorage: Seats));

        Assert.Contains("seats line or at least one proration", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyProrationsAndNoSeats_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PasswordManagerInvoiceItems(prorations: []));
    }
}

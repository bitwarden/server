namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>One purchasable's proration lines collapsed into a single renderable row. All amounts are dollars.</summary>
public record PurchasableProration
{
    /// <summary>The purchasable_reference this proration offsets (e.g. pm-seat).</summary>
    public required string Reference { get; init; }

    /// <summary>Absolute value of the negative line amounts.</summary>
    public required decimal Credit { get; init; }

    /// <summary>Sum of the positive line amounts.</summary>
    public required decimal Charge { get; init; }

    /// <summary>Sum of Stripe's per-line tax on this bucket's proration lines.</summary>
    public required decimal Tax { get; init; }

    /// <summary>Net of charge against credit.</summary>
    public required decimal Total { get; init; }

    public required int Months { get; init; }
}

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>One product's proration lines collapsed into a single renderable credit row. All amounts are dollars.</summary>
public record PurchasableProration
{
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

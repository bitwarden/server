using Bit.Core.Billing.Subscriptions.Models;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>The subscription-level envelope around a preview.</summary>
public record SubscriptionPreview
{
    /// <summary>A Stripe subscription status string. The client narrows it to a union.</summary>
    public required string Status { get; init; }

    public required InvoicePreview InvoicePreview { get; init; }

    /// <summary>Null for subscribers without max storage.</summary>
    public Storage? Storage { get; init; }

    public DateTime? CancelAt { get; init; }
    public DateTime? Canceled { get; init; }
    public DateTime? Suspension { get; init; }
    public int? GracePeriod { get; init; }

    /// <summary>A pending scheduled future-phase change (e.g. an annual switch at renewal). Set downstream, never by the projection.</summary>
    public PendingSubscriptionChange? PendingChange { get; init; }
}

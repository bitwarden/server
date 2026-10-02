using Bit.Core.Billing.Subscriptions.Models;

namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>The subscription-level envelope around a preview.</summary>
public record SubscriptionPreview
{
    /// <summary>A Stripe subscription status string. One of:
    /// incomplete, incomplete_expired, trialing, active, past_due, canceled, unpaid.</summary>
    public required string Status { get; init; }

    public required InvoicePreview InvoicePreview { get; init; }

    /// <summary>Null for subscribers without max storage.</summary>
    public Storage? Storage { get; init; }

    /// <summary>Scheduled cancellation date. Populated only for trialing/active, and optional even then.</summary>
    public DateTime? CancelAt { get; init; }

    /// <summary>When the subscription was canceled. Required when Status is canceled; null otherwise.</summary>
    public DateTime? Canceled { get; init; }

    /// <summary>When the subscription suspends. Required for incomplete/incomplete_expired; optional for past_due/unpaid; null otherwise.</summary>
    public DateTime? Suspension { get; init; }

    /// <summary>Grace-period days before suspension. Required for incomplete/incomplete_expired; optional for past_due/unpaid; null otherwise. A value of zero means the subscription suspends today, not that no grace period is set.</summary>
    public int? GracePeriod { get; init; }

    /// <summary>A pending scheduled future-phase change (e.g. an annual switch at renewal). Set downstream, never by the projection.</summary>
    public PendingSubscriptionChange? PendingChange { get; init; }
}

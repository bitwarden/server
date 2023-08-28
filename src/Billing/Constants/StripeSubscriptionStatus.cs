namespace Bit.Billing.Constants;

public static class StripeSubscriptionStatus
{
    public const string Trialing = "trialing";
    public const string Active = "active";
    public const string Incomplete = "incomplete";
    public const string IncompleteExpired = "incomplete_expired";
    public const string PastDue = "past_due";
    public const string Canceled = "canceled";
    public const string Unpaid = "unpaid";
    public const string Paused = "paused";
}

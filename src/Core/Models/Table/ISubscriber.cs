namespace Bit.Core.Models.Table
{
    public interface ISubscriber
    {
        string StripeCustomerId { get; set; }
        string StripeSubscriptionId { get; set; }
        string BillingEmailAddress();
        string BillingName();
    }
}

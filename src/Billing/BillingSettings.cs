namespace Bit.Billing
{
    public class BillingSettings
    {
        public virtual string StripeWebhookKey { get; set; }
        public virtual string StripeWebhookSecret { get; set; }
        public virtual string BraintreeWebhookKey { get; set; }
        public virtual string JobsKey { get; set; }
    }
}

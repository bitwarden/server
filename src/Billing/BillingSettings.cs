namespace Bit.Billing
{
    public class BillingSettings
    {
        public virtual string JobsKey { get; set; }
        public virtual string StripeWebhookKey { get; set; }
        public virtual string StripeWebhookSecret { get; set; }
        public virtual string BraintreeWebhookKey { get; set; }
        public virtual PaypalSettings Paypal { get; set; } = new PaypalSettings();

        public class PaypalSettings
        {
            public virtual bool Production { get; set; }
            public virtual string ClientId { get; set; }
            public virtual string ClientSecret { get; set; }
            public virtual string WebhookId { get; set; }
        }
    }
}

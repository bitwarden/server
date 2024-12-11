namespace Bit.Billing;

public class BillingSettings
{
    public virtual string JobsKey { get; set; }
    public virtual string StripeWebhookKey { get; set; }
    public virtual string StripeWebhookSecret { get; set; }
    public virtual string StripeWebhookSecret20231016 { get; set; }
    public virtual string StripeWebhookSecret20240620 { get; set; }
    public virtual string BitPayWebhookKey { get; set; }
    public virtual string AppleWebhookKey { get; set; }
    public virtual FreshDeskSettings FreshDesk { get; set; } = new FreshDeskSettings();
    public virtual string FreshsalesApiKey { get; set; }
    public virtual PayPalSettings PayPal { get; set; } = new PayPalSettings();

    public class PayPalSettings
    {
        public virtual bool Production { get; set; }
        public virtual string BusinessId { get; set; }
        public virtual string WebhookKey { get; set; }
    }

    public class FreshDeskSettings
    {
        public virtual string ApiKey { get; set; }
        public virtual string WebhookKey { get; set; }

        /// <summary>
        /// Indicates the data center region. Valid values are "US" and "EU"
        /// </summary>
        public virtual string Region { get; set; }
        public virtual string UserFieldName { get; set; }
        public virtual string OrgFieldName { get; set; }
    }
}

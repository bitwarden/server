// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Billing;

public class BillingSettings
{
    public virtual string JobsKey { get; set; }
    public virtual string StripeWebhookKey { get; set; }
    public virtual string StripeWebhookSecret20250827Basil { get; set; }
    public virtual string AppleWebhookKey { get; set; }
    public virtual PayPalSettings PayPal { get; set; } = new PayPalSettings();

    public class PayPalSettings
    {
        public virtual bool Production { get; set; }
        public virtual string BusinessId { get; set; }
        public virtual string WebhookKey { get; set; }
    }

}

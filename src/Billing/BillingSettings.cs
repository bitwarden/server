﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Billing;

public class BillingSettings
{
    public virtual string JobsKey { get; set; }
    public virtual string StripeWebhookKey { get; set; }
    public virtual string StripeWebhookSecret20250827Basil { get; set; }
    public virtual string AppleWebhookKey { get; set; }
    public virtual FreshDeskSettings FreshDesk { get; set; } = new FreshDeskSettings();
    public virtual string FreshsalesApiKey { get; set; }
    public virtual PayPalSettings PayPal { get; set; } = new PayPalSettings();
    public virtual OnyxSettings Onyx { get; set; } = new OnyxSettings();

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

        public virtual bool RemoveNewlinesInReplies { get; set; } = false;
        public virtual string AutoReplyGreeting { get; set; } = string.Empty;
        public virtual string AutoReplySalutation { get; set; } = string.Empty;
    }

    public class OnyxSettings
    {
        public virtual string ApiKey { get; set; }
        public virtual string BaseUrl { get; set; }
        public virtual string Path { get; set; }
        public virtual int PersonaId { get; set; }
        public virtual bool UseAnswerWithCitationModels { get; set; } = true;

        public virtual SearchSettings SearchSettings { get; set; } = new SearchSettings();
    }
    public class SearchSettings
    {
        public virtual string RunSearch { get; set; } = "auto"; // "always", "never", "auto"
        public virtual bool RealTime { get; set; } = true;
    }
}

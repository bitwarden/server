using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Constants;

public static class StripeConstants
{
    public static class AutomaticTaxStatus
    {
        public const string Failed = "failed";
        public const string NotCollecting = "not_collecting";
        public const string Supported = "supported";
        public const string UnrecognizedLocation = "unrecognized_location";
    }

    public static class BillingReasons
    {
        public const string SubscriptionCreate = "subscription_create";
        public const string SubscriptionCycle = "subscription_cycle";
    }

    public static class CollectionMethod
    {
        public const string ChargeAutomatically = "charge_automatically";
        public const string SendInvoice = "send_invoice";
    }

    public static class CouponIDs
    {
        public const string LegacyMSPDiscount = "msp-discount-35";
        public const string SecretsManagerStandalone = "sm-standalone";
        public const string Milestone2SubscriptionDiscount = "milestone-2c";
        public const string Milestone3SubscriptionDiscount = "milestone-3";

        public static class MSPDiscounts
        {
            public const string Open = "msp-open-discount";
            public const string Silver = "msp-silver-discount";
            public const string Gold = "msp-gold-discount";
        }
    }

    public static class CouponExpandablePropertyNames
    {
        public const string AppliesTo = "applies_to";
    }

    public static class ErrorCodes
    {
        public const string CustomerTaxLocationInvalid = "customer_tax_location_invalid";
        public const string InvoiceUpcomingNone = "invoice_upcoming_none";
        public const string PaymentMethodMicroDepositVerificationAttemptsExceeded = "payment_method_microdeposit_verification_attempts_exceeded";
        public const string PaymentMethodMicroDepositVerificationDescriptorCodeMismatch = "payment_method_microdeposit_verification_descriptor_code_mismatch";
        public const string PaymentMethodMicroDepositVerificationTimeout = "payment_method_microdeposit_verification_timeout";
        public const string ResourceMissing = "resource_missing";
        public const string TaxIdInvalid = "tax_id_invalid";

        public static string[] InputErrors() =>
        [
            CustomerTaxLocationInvalid,
            InvoiceUpcomingNone,
            PaymentMethodMicroDepositVerificationAttemptsExceeded,
            PaymentMethodMicroDepositVerificationDescriptorCodeMismatch,
            PaymentMethodMicroDepositVerificationTimeout,
            TaxIdInvalid
        ];
    }

    public static class Intervals
    {
        public const string Month = "month";
        public const string Year = "year";
    }

    public static class InvoiceStatus
    {
        public const string Draft = "draft";
        public const string Open = "open";
        public const string Paid = "paid";
    }

    public static class MetadataKeys
    {
        public const string BraintreeCustomerId = "btCustomerId";
        public const string BraintreeTransactionId = "btTransactionId";
        public const string InvoiceApproved = "invoice_approved";
        public const string OrganizationId = "organizationId";
        public const string PayPalTransactionId = "btPayPalTransactionId";
        public const string ProviderId = "providerId";
        public const string Region = "region";
        public const string RetiredBraintreeCustomerId = "btCustomerId_old";
        public const string UserId = "userId";
        public const string StorageReconciled2025 = "storage_reconciled_2025";
        public const string OriginatingPlatform = "originatingPlatform";
        public const string OriginatingAppVersion = "originatingAppVersion";
        public const string TrialInitiationPath = "trialInitiationPath";
        public const string CancelledDuringDeferredPriceIncrease = "cancelled_during_deferred_price_increase";
    }

    public static class PaymentBehavior
    {
        public const string DefaultIncomplete = "default_incomplete";
        public const string PendingIfIncomplete = "pending_if_incomplete";
    }

    public static class PaymentMethodTypes
    {
        public const string Card = "card";
        public const string USBankAccount = "us_bank_account";
    }

    public static class Prices
    {
        public const string StoragePlanPersonal = "personal-storage-gb-annually";
        public const string PremiumAnnually = "premium-annually";
    }

    public static class ProrationBehavior
    {
        public const string AlwaysInvoice = "always_invoice";
        public const string CreateProrations = "create_prorations";
        public const string None = "none";
    }

    public static class SubscriptionScheduleEndBehavior
    {
        public const string Cancel = "cancel";
        public const string None = "none";
        public const string Release = "release";
        public const string Renew = "renew";
    }

    public static class SubscriptionScheduleStatus
    {
        public const string Active = "active";
        public const string Canceled = "canceled";
        public const string Completed = "completed";
        public const string NotStarted = "not_started";
        public const string Released = "released";
    }

    public static class SubscriptionStatus
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

    public static class TaxExempt
    {
        public const string Exempt = "exempt";
        public const string None = "none";
        public const string Reverse = "reverse";
    }

    public static class TaxIdType
    {
        public const string EUVAT = "eu_vat";
        public const string SpanishNIF = "es_cif";
    }

    public static class TaxIdVerificationStatus
    {
        public const string Pending = "pending";
        public const string Unavailable = "unavailable";
        public const string Unverified = "unverified";
        public const string Verified = "verified";
    }

    public static class TaxRegistrationStatus
    {
        public const string Active = "active";
        public const string Expired = "expired";
        public const string Scheduled = "scheduled";
    }

    public static class ValidateTaxLocationTiming
    {
        public const string Deferred = "deferred";
        public const string Immediately = "immediately";
    }

    public static class MissingPaymentMethodBehaviorOptions
    {
        public const string CreateInvoice = "create_invoice";
        public const string Cancel = "cancel";
        public const string Pause = "pause";
    }
    /// <summary>
    /// Product Ids in Stripe that are used to identify password manager products in subscriptions
    /// These should be kept up to date with the products created in Stripe dashboard.
    /// </summary>
    public static class ProductIDs
    {
        public const string Premium = "prod_BUqgYr48VzDuCg";
        public const string Families = "prod_HgOroKDcpTzJgn";

        /// <summary>
        /// Gets the product tier for a given Stripe product ID.
        /// </summary>
        /// <param name="productId">The Stripe product ID.</param>
        /// <returns>The corresponding <see cref="DiscountTierType"/>, or <see langword="null"/> if not found.</returns>
        public static DiscountTierType? GetProductTier(string productId) => productId switch
        {
            Premium => DiscountTierType.Premium,
            Families => DiscountTierType.Families,
            _ => null
        };
    }

    public static class CheckoutSession
    {
        public static class Modes
        {
            public const string Subscription = "subscription";
            public const string Payment = "payment";
            public const string Setup = "setup";
        }

        // https://docs.stripe.com/api/checkout/sessions/create#create_checkout_session-customer_update-address
        // Determines whether the customer's address should be updated during checkout session or not.
        public static class CustomerUpdateAddressOptions
        {
            public const string Auto = "auto";
            public const string Never = "never";
        }

        public static class Platforms
        {
            public const string Ios = "ios";
            public const string Android = "android";
        }
    }

}

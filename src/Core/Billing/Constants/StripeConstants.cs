﻿namespace Bit.Core.Billing.Constants;

public static class StripeConstants
{
    public static class AutomaticTaxStatus
    {
        public const string Failed = "failed";
        public const string NotCollecting = "not_collecting";
        public const string Supported = "supported";
        public const string UnrecognizedLocation = "unrecognized_location";
    }

    public static class CollectionMethod
    {
        public const string ChargeAutomatically = "charge_automatically";
        public const string SendInvoice = "send_invoice";
    }

    public static class CouponIDs
    {
        public const string SecretsManagerStandalone = "sm-standalone";
    }

    public static class ErrorCodes
    {
        public const string CustomerTaxLocationInvalid = "customer_tax_location_invalid";
        public const string ResourceMissing = "resource_missing";
        public const string TaxIdInvalid = "tax_id_invalid";
    }

    public static class PaymentMethodTypes
    {
        public const string Card = "card";
        public const string USBankAccount = "us_bank_account";
    }

    public static class ProrationBehavior
    {
        public const string AlwaysInvoice = "always_invoice";
        public const string CreateProrations = "create_prorations";
        public const string None = "none";
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
}

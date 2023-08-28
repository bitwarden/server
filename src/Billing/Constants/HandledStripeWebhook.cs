namespace Bit.Billing.Constants;

public static class HandledStripeWebhook
{
    public const string SubscriptionDeleted = "customer.subscription.deleted";
    public const string SubscriptionUpdated = "customer.subscription.updated";
    public const string UpcomingInvoice = "invoice.upcoming";
    public const string ChargeSucceeded = "charge.succeeded";
    public const string ChargeRefunded = "charge.refunded";
    public const string PaymentSucceeded = "invoice.payment_succeeded";
    public const string PaymentFailed = "invoice.payment_failed";
    public const string InvoiceCreated = "invoice.created";
    public const string PaymentMethodAttached = "payment_method.attached";
}

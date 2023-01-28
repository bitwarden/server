namespace Bit.Billing.Constants;

public static class HandledStripeWebhook
{
    public static string SubscriptionDeleted => "customer.subscription.deleted";
    public static string SubscriptionUpdated => "customer.subscription.updated";
    public static string UpcomingInvoice => "invoice.upcoming";
    public static string ChargeSucceeded => "charge.succeeded";
    public static string ChargeRefunded => "charge.refunded";
    public static string PaymentSucceeded => "invoice.payment_succeeded";
    public static string PaymentFailed => "invoice.payment_failed";
    public static string InvoiceCreated => "invoice.created";
}

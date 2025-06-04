namespace Bit.Core.Billing.Models;

[Obsolete("This concept is being phased out. Don't add additional properties.")]
public record OrganizationMetadata(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    bool IsSubscriptionUnpaid,
    bool HasSubscription,
    bool HasOpenInvoice,
    bool IsSubscriptionCanceled,
    DateTime? InvoiceDueDate,
    DateTime? InvoiceCreatedDate,
    DateTime? SubPeriodEndDate)
{
    public static OrganizationMetadata Default => new OrganizationMetadata(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        null,
        null,
        null);
}

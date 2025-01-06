using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    bool IsSubscriptionUnpaid,
    bool HasSubscription,
    bool HasOpenInvoice,
    DateTime? InvoiceDueDate,
    DateTime? InvoiceCreatedDate,
    DateTime? SubPeriodEndDate)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata)
        => new(
            metadata.IsEligibleForSelfHost,
            metadata.IsManaged,
            metadata.IsOnSecretsManagerStandalone,
            metadata.IsSubscriptionUnpaid,
            metadata.HasSubscription,
            metadata.HasOpenInvoice,
            metadata.InvoiceDueDate,
            metadata.InvoiceCreatedDate,
            metadata.SubPeriodEndDate);
}

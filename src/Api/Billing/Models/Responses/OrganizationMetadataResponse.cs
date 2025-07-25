﻿using Bit.Core.Billing.Organizations.Models;

namespace Bit.Api.Billing.Models.Responses;

public record OrganizationMetadataResponse(
    bool IsEligibleForSelfHost,
    bool IsManaged,
    bool IsOnSecretsManagerStandalone,
    bool IsSubscriptionUnpaid,
    bool HasSubscription,
    bool HasOpenInvoice,
    bool IsSubscriptionCanceled,
    DateTime? InvoiceDueDate,
    DateTime? InvoiceCreatedDate,
    DateTime? SubPeriodEndDate,
    int OrganizationOccupiedSeats)
{
    public static OrganizationMetadataResponse From(OrganizationMetadata metadata)
        => new(
            metadata.IsEligibleForSelfHost,
            metadata.IsManaged,
            metadata.IsOnSecretsManagerStandalone,
            metadata.IsSubscriptionUnpaid,
            metadata.HasSubscription,
            metadata.HasOpenInvoice,
            metadata.IsSubscriptionCanceled,
            metadata.InvoiceDueDate,
            metadata.InvoiceCreatedDate,
            metadata.SubPeriodEndDate,
            metadata.OrganizationOccupiedSeats);
}

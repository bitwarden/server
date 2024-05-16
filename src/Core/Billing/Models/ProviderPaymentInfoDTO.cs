using Bit.Core.Models.Business;

public record ProviderPaymentInfoDTO(
    BillingInfo.BillingSource billingSource,
    TaxInfo taxInfo);

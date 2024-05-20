using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Models;

public record ProviderPaymentInfoDTO(BillingInfo.BillingSource billingSource,
    TaxInfo taxInfo);

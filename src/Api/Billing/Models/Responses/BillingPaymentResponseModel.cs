using Bit.Core.Billing.Models;
using Bit.Core.Models.Api;

namespace Bit.Api.Billing.Models.Responses;

public class BillingPaymentResponseModel : ResponseModel
{
    public BillingPaymentResponseModel(BillingInfo billing)
        : base("billingPayment")
    {
        Balance = billing.Balance;
        PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
    }

    public decimal Balance { get; set; }
    public BillingSource PaymentSource { get; set; }
}

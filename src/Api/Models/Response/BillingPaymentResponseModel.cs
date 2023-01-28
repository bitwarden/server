using Bit.Core.Models.Api;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Response;

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

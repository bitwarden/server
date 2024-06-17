using Bit.Core.Billing.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Billing.Models.Responses;

public class BillingResponseModel : ResponseModel
{
    public BillingResponseModel(BillingInfo billing)
        : base("billing")
    {
        Balance = billing.Balance;
        PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
    }

    public decimal Balance { get; set; }
    public BillingSource PaymentSource { get; set; }
}

public class BillingSource
{
    public BillingSource(BillingInfo.BillingSource source)
    {
        Type = source.Type;
        CardBrand = source.CardBrand;
        Description = source.Description;
        NeedsVerification = source.NeedsVerification;
    }

    public PaymentMethodType Type { get; set; }
    public string CardBrand { get; set; }
    public string Description { get; set; }
    public bool NeedsVerification { get; set; }
}

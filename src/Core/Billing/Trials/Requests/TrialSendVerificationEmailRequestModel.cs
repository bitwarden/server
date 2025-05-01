using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Billing.Pricing.Enums;

namespace Bit.Core.Billing.Trials.Requests;

public class TrialSendVerificationEmailRequestModel : RegisterSendVerificationEmailRequestModel
{
    public ProductTierType ProductTier { get; set; }
    public IEnumerable<ProductType> Products { get; set; }
}

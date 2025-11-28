// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models.Api.Requests.Accounts;

public class TrialSendVerificationEmailRequestModel : RegisterSendVerificationEmailRequestModel
{
    public ProductTierType ProductTier { get; set; }
    public IEnumerable<ProductType> Products { get; set; }
    public int? TrialLength { get; set; }
}

// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class PaymentResponseModel : ResponseModel
{
    public PaymentResponseModel()
        : base("payment")
    { }

    public ProfileResponseModel UserProfile { get; set; }
    public string PaymentIntentClientSecret { get; set; }
    public bool Success { get; set; }
}

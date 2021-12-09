using Bit.Core.Models.Api;

namespace Bit.Web.Models.Api
{
    public class PaymentResponseModel : ResponseModel
    {
        public PaymentResponseModel()
            : base("payment")
        { }

        public ProfileResponseModel UserProfile { get; set; }
        public string PaymentIntentClientSecret { get; set; }
        public bool Success { get; set; }
    }
}

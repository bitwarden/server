namespace Bit.Core.Models.Api
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

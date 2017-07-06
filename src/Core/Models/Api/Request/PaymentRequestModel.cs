using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class PaymentRequestModel
    {
        [Required]
        public string PaymentToken { get; set; }
    }
}

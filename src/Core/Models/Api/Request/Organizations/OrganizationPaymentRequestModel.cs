using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationPaymentRequestModel
    {
        [Required]
        public string PaymentToken { get; set; }
    }
}

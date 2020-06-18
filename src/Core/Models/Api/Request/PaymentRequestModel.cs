using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class PaymentRequestModel : OrganizationTaxInfoUpdateRequestModel
    {
        [Required]
        public PaymentMethodType? PaymentMethodType { get; set; }
        [Required]
        public string PaymentToken { get; set; }
    }
}

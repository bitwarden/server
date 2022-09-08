using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request;

public class PaymentRequestModel : OrganizationTaxInfoUpdateRequestModel
{
    [Required]
    public PaymentMethodType? PaymentMethodType { get; set; }
    [Required]
    public string PaymentToken { get; set; }
}

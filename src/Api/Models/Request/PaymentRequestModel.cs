// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request;

public class PaymentRequestModel : ExpandedTaxInfoUpdateRequestModel
{
    [Required]
    public PaymentMethodType? PaymentMethodType { get; set; }
    [Required]
    public string PaymentToken { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class VerifyBankAccountRequest
{
    [Required]
    public string DescriptorCode { get; set; }
}

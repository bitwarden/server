using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class VerifyBankAccountRequestBody
{
    [Required]
    public string DescriptorCode { get; set; }
}

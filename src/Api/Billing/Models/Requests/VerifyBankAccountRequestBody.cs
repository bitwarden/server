// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class VerifyBankAccountRequestBody
{
    [Required]
    public string DescriptorCode { get; set; }
}

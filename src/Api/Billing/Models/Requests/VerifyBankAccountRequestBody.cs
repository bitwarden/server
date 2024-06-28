using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class VerifyBankAccountRequestBody
{
    [Range(0, 99)]
    public long Amount1 { get; set; }
    [Range(0, 99)]
    public long Amount2 { get; set; }
}

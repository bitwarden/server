using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests;

public class VerifyBankAccountRequestBody
{
    [Range(-99, 99)]
    public long Amount1 { get; set; }
    [Range(-99, 99)]
    public long Amount2 { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Bit.Api.Billing.Attributes;
using Bit.Core.Billing.Payment.Models;

namespace Bit.Api.Billing.Models.Requests.Payment;

public class NonTokenizedPaymentMethodRequest
{
    [Required]
    [NonTokenizedPaymentMethodTypeValidation]
    public required string Type { get; set; }

    public NonTokenizedPaymentMethod ToDomain()
    {
        return Type switch
        {
            "accountCredit" => new NonTokenizedPaymentMethod { Type = NonTokenizablePaymentMethodType.AccountCredit },
            _ => throw new InvalidOperationException($"Invalid value for {nameof(NonTokenizedPaymentMethod)}.{nameof(NonTokenizedPaymentMethod.Type)}")
        };
    }
}

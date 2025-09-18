using Bit.Api.Utilities;

namespace Bit.Api.Billing.Attributes;

public class PaymentMethodTypeValidationAttribute : StringMatchesAttribute
{
    private static readonly string[] _acceptedValues = ["bankAccount", "card", "payPal"];

    public PaymentMethodTypeValidationAttribute() : base(_acceptedValues)
    {
        ErrorMessage = $"Payment method type must be one of: {string.Join(", ", _acceptedValues)}";
    }
}

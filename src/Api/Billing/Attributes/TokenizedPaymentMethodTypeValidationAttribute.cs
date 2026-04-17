using Bit.Api.Utilities;

namespace Bit.Api.Billing.Attributes;

public class TokenizedPaymentMethodTypeValidationAttribute : StringMatchesAttribute
{
    private static readonly string[] _acceptedValues = ["bankAccount", "card", "payPal"];

    public TokenizedPaymentMethodTypeValidationAttribute() : base(_acceptedValues)
    {
        ErrorMessage = $"Payment method type must be one of: {string.Join(", ", _acceptedValues)}";
    }
}

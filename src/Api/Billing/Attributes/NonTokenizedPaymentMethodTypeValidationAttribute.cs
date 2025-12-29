using Bit.Api.Utilities;

namespace Bit.Api.Billing.Attributes;

public class NonTokenizedPaymentMethodTypeValidationAttribute : StringMatchesAttribute
{
    private static readonly string[] _acceptedValues = ["accountCredit"];

    public NonTokenizedPaymentMethodTypeValidationAttribute() : base(_acceptedValues)
    {
        ErrorMessage = $"Payment method type must be one of: {string.Join(", ", _acceptedValues)}";
    }
}

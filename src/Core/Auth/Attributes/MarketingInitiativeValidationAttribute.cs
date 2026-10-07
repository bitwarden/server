using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;

namespace Bit.Core.Auth.Attributes;

public class MarketingInitiativeValidationAttribute : ValidationAttribute
{
    private static readonly string[] _acceptedValues = [MarketingInitiativeConstants.Premium];

    public MarketingInitiativeValidationAttribute()
    {
        ErrorMessage = $"Marketing initiative type must be one of: {string.Join(", ", _acceptedValues)}";
    }

    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is not string str)
        {
            return false;
        }

        return _acceptedValues.Contains(str);
    }
}

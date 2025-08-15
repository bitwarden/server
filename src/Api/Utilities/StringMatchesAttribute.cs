using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Utilities;

public class StringMatchesAttribute(params string[]? accepted) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string str ||
            accepted == null ||
            accepted.Length == 0)
        {
            return false;
        }

        return accepted.Contains(str);
    }
}

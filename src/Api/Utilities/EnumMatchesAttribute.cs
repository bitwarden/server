using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Utilities;

public class EnumMatchesAttribute<T>(params T[] accepted) : ValidationAttribute
    where T : Enum
{
    public override bool IsValid(object value)
    {
        if (value == null || accepted == null || accepted.Length == 0)
        {
            return false;
        }

        var success = Enum.TryParse(typeof(T), value.ToString(), out var result);

        if (!success)
        {
            return false;
        }

        var typed = (T)result;

        return accepted.Contains(typed);
    }
}

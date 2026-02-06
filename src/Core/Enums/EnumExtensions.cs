using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Bit.Core.Enums;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field?.GetCustomAttribute<DisplayAttribute>() is { } attribute)
        {
            return attribute.Name ?? value.ToString();
        }

        return value.ToString();
    }
}

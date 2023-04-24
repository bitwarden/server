using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Bit.SharedWeb.Utilities;

public static class DisplayAttributeHelpers
{
    public static DisplayAttribute GetDisplayAttribute(this Enum enumValue)
    {
        return enumValue.GetType()
            .GetMember(enumValue.ToString())
            .First()
            .GetCustomAttribute<DisplayAttribute>();
    }

    public static DisplayAttribute GetDisplayAttribute<T>(this string property)
    {
        MemberInfo propertyInfo = typeof(T).GetProperty(property);
        return propertyInfo?.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;
    }
}

using System.Reflection;
using System.Runtime.Serialization;

namespace Bit.Subscriptions.Organization.Models.Requests;

/// <summary>
/// A record struct that represents a parameter for an enum member.
/// It implements the IParsable interface to allow parsing from a string representation of the enum member's value.
/// </summary>
/// <param name="Value"></param>
/// <typeparam name="T"></typeparam>
internal readonly record struct EnumMemberParameter<T>(T Value) : IParsable<EnumMemberParameter<T>>
    where T : struct, Enum
{
    public static EnumMemberParameter<T> Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException($"'{s}' is not a valid {typeof(T).Name}.");

    public static bool TryParse(string? s, IFormatProvider? provider, out EnumMemberParameter<T> result)
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (string.Equals(field.GetCustomAttribute<EnumMemberAttribute>()?.Value, s,
                    StringComparison.OrdinalIgnoreCase))
            {
                result = new EnumMemberParameter<T>((T)field.GetValue(null)!);
                return true;
            }
        }

        result = default;
        return false;
    }
}

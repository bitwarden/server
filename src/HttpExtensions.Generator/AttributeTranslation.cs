using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Bit.HttpExtensions.Generator;

/// <summary>One validation attribute, read at compile time and turned into the code it should report.</summary>
/// <param name="Parameters">Substitution name to the C# expression producing its value.</param>
/// <param name="Construction">
/// A C# expression reconstructing the attribute, used to ask it how it words its message. Null when the
/// attribute's constructor is itself unsafe under trimming, in which case this candidate can only be identified
/// by elimination.
/// </param>
internal sealed record AttributeTranslation(
    string Code,
    IReadOnlyList<KeyValuePair<string, string>> Parameters,
    string? Construction);

internal static class AttributeTranslator
{
    private const string Ns = "System.ComponentModel.DataAnnotations";

    public static bool IsValidationAttribute(AttributeData attribute)
    {
        for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
        {
            if (type.ToDisplayString() == $"{Ns}.ValidationAttribute")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Translates one attribute, or returns null when it is not one we have a name for.</summary>
    public static AttributeTranslation? Translate(AttributeData attribute)
    {
        var name = attribute.AttributeClass?.ToDisplayString();
        if (name is null)
        {
            return null;
        }

        var parameters = new List<KeyValuePair<string, string>>();
        string code;
        string? construction;

        switch (name)
        {
            case $"{Ns}.RequiredAttribute":
                code = "required";
                construction = $"new global::{Ns}.RequiredAttribute()";
                break;

            case $"{Ns}.StringLengthAttribute":
                {
                    var max = Ctor(attribute, 0)?.Value;
                    var min = Named(attribute, "MinimumLength")?.Value;
                    var bounded = min is int lower && lower > 0;

                    if (bounded)
                    {
                        // One message covers both directions, so the two cannot be told apart. Report the constraint
                        // rather than guessing a direction; the client composes from both bounds.
                        code = "invalid_length";
                        parameters.Add(new("min", Literal(min)));
                        parameters.Add(new("max", Literal(max)));
                        construction =
                            $"new global::{Ns}.StringLengthAttribute({Literal(max)}) {{ MinimumLength = {Literal(min)} }}";
                    }
                    else
                    {
                        code = "too_long";
                        parameters.Add(new("max", Literal(max)));
                        construction = $"new global::{Ns}.StringLengthAttribute({Literal(max)})";
                    }

                    break;
                }

            // MaxLength, MinLength and Compare have [RequiresUnreferencedCode] constructors, so generated code
            // cannot build them to ask for their wording. They are identified by elimination instead.
            case $"{Ns}.MaxLengthAttribute":
                code = "too_long";
                parameters.Add(new("max", Literal(Ctor(attribute, 0)?.Value)));
                construction = null;
                break;

            case $"{Ns}.MinLengthAttribute":
                code = "too_short";
                parameters.Add(new("min", Literal(Ctor(attribute, 0)?.Value)));
                construction = null;
                break;

            case $"{Ns}.CompareAttribute":
                code = "must_match";
                parameters.Add(new("other", Literal(Ctor(attribute, 0)?.Value)));
                construction = null;
                break;

            case $"{Ns}.RangeAttribute":
                {
                    // The (Type, string, string) overload states its bounds as strings; the numeric ones do not.
                    var first = Ctor(attribute, 0)?.Value;
                    var second = Ctor(attribute, 1)?.Value;
                    var third = Ctor(attribute, 2)?.Value;
                    var (min, max) = third is null ? (first, second) : (second, third);

                    code = "out_of_range";
                    parameters.Add(new("min", Literal(min)));
                    parameters.Add(new("max", Literal(max)));
                    construction = third is null
                        ? $"new global::{Ns}.RangeAttribute({Literal(min)}, {Literal(max)})"
                        : null;
                    break;
                }

            case $"{Ns}.EmailAddressAttribute":
                code = "invalid_email";
                construction = $"new global::{Ns}.EmailAddressAttribute()";
                break;

            case $"{Ns}.RegularExpressionAttribute":
                {
                    var pattern = Ctor(attribute, 0)?.Value;
                    code = "invalid_format";
                    parameters.Add(new("pattern", Literal(pattern)));
                    construction = $"new global::{Ns}.RegularExpressionAttribute({Literal(pattern)})";
                    break;
                }

            case $"{Ns}.UrlAttribute":
                code = "invalid_format";
                construction = $"new global::{Ns}.UrlAttribute()";
                break;

            case $"{Ns}.PhoneAttribute":
                code = "invalid_format";
                construction = $"new global::{Ns}.PhoneAttribute()";
                break;

            case $"{Ns}.CreditCardAttribute":
                code = "invalid_format";
                construction = $"new global::{Ns}.CreditCardAttribute()";
                break;

            default:
                return null;
        }

        // The wording depends on ErrorMessage when it is set, so the reconstruction has to carry it too.
        if (construction is not null && Named(attribute, "ErrorMessage")?.Value is string explicitMessage)
        {
            construction = WithInitializer(construction, $"ErrorMessage = {Quote(explicitMessage)}");
        }
        else if (construction is not null &&
                 (Named(attribute, "ErrorMessageResourceName") is not null ||
                  Named(attribute, "ErrorMessageResourceType") is not null))
        {
            // Resource-backed wording cannot be reproduced from metadata alone.
            construction = null;
        }

        return new AttributeTranslation(code, parameters, construction);
    }

    /// <summary>Merges another assignment into an object initializer, adding one if it has none.</summary>
    private static string WithInitializer(string construction, string assignment)
    {
        var brace = construction.LastIndexOf('{');
        return brace < 0
            ? construction + $" {{ {assignment} }}"
            : construction.Insert(brace + 1, $" {assignment},");
    }

    private static TypedConstant? Ctor(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index] : null;

    private static TypedConstant? Named(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Key == name)
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>The value as a C# expression.</summary>
    public static string Literal(object? value) => value switch
    {
        null => "null",
        string s => Quote(s),
        bool b => b ? "true" : "false",
        double d => d.ToString("R", CultureInfo.InvariantCulture) + "d",
        float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
        decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
        long l => l.ToString(CultureInfo.InvariantCulture) + "L",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null",
    };

    public static string Quote(string value) =>
        "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n") + "\"";
}

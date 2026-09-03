using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bit.HttpExtensions;

/// <summary>
/// Names a validation failure by recognising the message the framework recorded for it.
/// </summary>
/// <remarks>
/// <para>
/// DataAnnotations records a message and discards the constraint that produced it, so the only thing left to work
/// from is the sentence — which does at least still contain the limit that was breached. Each pattern below
/// recognises one attribute's wording and lifts its values back out.
/// </para>
/// <para>
/// Nothing here validates anything, and nothing here reflects. The framework decides whether a value is valid;
/// this only reads what it said afterwards. Being pure string work, it survives trimming and ahead-of-time
/// publishing with no annotations and no generated lookup.
/// </para>
/// <para>
/// The cost is that the wording is the contract. A framework release that rewrites a message stops it being
/// recognised, and the failure is reported as <see cref="ValidationCodes.Invalid"/> with its message intact
/// rather than under a wrong code. <c>ValidationMessageCodesTests</c> asserts every pattern against the message
/// its attribute actually produces, so a reword fails the build on the next SDK bump instead of in production.
/// </para>
/// </remarks>
public static partial class ValidationMessageCodes
{
    /// <summary>
    /// The code and parameters for <paramref name="message"/>, or <see cref="ValidationCodes.Invalid"/> when no
    /// pattern claims it.
    /// </summary>
    /// <remarks>
    /// Always answers. An unrecognised message keeps its detail and loses only its code, because a failure that
    /// reached the client uncoded is still a failure it can read.
    /// </remarks>
    public static ErrorCode Resolve(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (Required().IsMatch(message))
        {
            return new ErrorCode(ValidationCodes.Required, message);
        }

        if (BoundedLength().Match(message) is { Success: true } bounded)
        {
            // One message covers both directions of a two-ended constraint, so there is nothing to tell them
            // apart. The client gets both bounds and composes the sentence itself.
            return Coded(ValidationCodes.InvalidLength, message,
                (ValidationParameters.Min, bounded.Groups[1].Value),
                (ValidationParameters.Max, bounded.Groups[2].Value));
        }

        if (MaximumLength().Match(message) is { Success: true } tooLong)
        {
            return Coded(ValidationCodes.TooLong, message, (ValidationParameters.Max, tooLong.Groups[1].Value));
        }

        if (MinimumLength().Match(message) is { Success: true } tooShort)
        {
            return Coded(ValidationCodes.TooShort, message, (ValidationParameters.Min, tooShort.Groups[1].Value));
        }

        if (Range().Match(message) is { Success: true } range)
        {
            return Coded(ValidationCodes.OutOfRange, message,
                (ValidationParameters.Min, range.Groups[1].Value),
                (ValidationParameters.Max, range.Groups[2].Value));
        }

        if (EmailAddress().IsMatch(message))
        {
            return new ErrorCode(ValidationCodes.InvalidEmail, message);
        }

        if (Pattern().Match(message) is { Success: true } pattern)
        {
            return Coded(ValidationCodes.InvalidFormat, message,
                (ValidationParameters.Pattern, pattern.Groups[1].Value));
        }

        if (Compare().Match(message) is { Success: true } compare)
        {
            return Coded(ValidationCodes.MustMatch, message, (ValidationParameters.Other, compare.Groups[1].Value));
        }

        if (Url().IsMatch(message) || Phone().IsMatch(message) || CreditCard().IsMatch(message))
        {
            return new ErrorCode(ValidationCodes.InvalidFormat, message);
        }

        return new ErrorCode(ValidationCodes.Invalid, message);
    }

    private static ErrorCode Coded(string code, string message, params (string Name, string Value)[] parameters)
    {
        var bag = new JsonObject();
        foreach (var (name, value) in parameters)
        {
            bag[name] = Number(value);
        }

        return new ErrorCode(code, message, bag);
    }

    /// <summary>
    /// The bound as the closest thing it reads as.
    /// </summary>
    /// <remarks>
    /// Lifted out of prose, so a bound that is not a number is carried as the text it was written as — a date
    /// range states its bounds as dates, and inventing a number for them would be worse than passing them along.
    /// </remarks>
    private static JsonNode? Number(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
        {
            return JsonValue.Create(whole);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wide))
        {
            return JsonValue.Create(wide);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return JsonValue.Create(real);
        }

        return JsonValue.Create(value);
    }

    [GeneratedRegex(@"^The (?:.+) field is required\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Required();

    [GeneratedRegex(
        @"^The field (?:.+) must be a string with a minimum length of (.+) and a maximum length of (.+)\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex BoundedLength();

    [GeneratedRegex(
        @"^The field (?:.+) must be a string (?:or array type )?with a maximum length of '?(.+?)'?\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex MaximumLength();

    [GeneratedRegex(
        @"^The field (?:.+) must be a string (?:or array type )?with a minimum length of '?(.+?)'?\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex MinimumLength();

    [GeneratedRegex(@"^The field (?:.+) must be between (.+) and (.+)\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Range();

    [GeneratedRegex(@"^The (?:.+) field is not a valid e-mail address\.$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddress();

    [GeneratedRegex(@"^The field (?:.+) must match the regular expression '(.+)'\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^'(?:.+)' and '(.+)' do not match\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Compare();

    [GeneratedRegex(
        @"^The (?:.+) field is not a valid fully-qualified http, https, or ftp URL\.$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Url();

    [GeneratedRegex(@"^The (?:.+) field is not a valid phone number\.$", RegexOptions.CultureInvariant)]
    private static partial Regex Phone();

    [GeneratedRegex(@"^The (?:.+) field is not a valid credit card number\.$", RegexOptions.CultureInvariant)]
    private static partial Regex CreditCard();
}

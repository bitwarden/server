namespace Bit.HttpExtensions;

/// <summary>
/// The repo-wide vocabulary of validation codes. A code names <em>what went wrong</em>; the property it is keyed
/// under names <em>where</em>, so a code never repeats the field it describes — <c>required</c>, not
/// <c>name_required</c>.
/// </summary>
/// <remarks>
/// <para>
/// Codes are scoped to their property, so the same code under two properties means the same thing in both, and a
/// client can handle <c>required</c> generically wherever it appears rather than learning one spelling per field.
/// </para>
/// <para>
/// Two validators catching the same condition must answer with the same code — that is the point of a shared
/// list. Add a constant here before inventing a spelling locally.
/// </para>
/// </remarks>
public static class ValidationCodes
{
    /// <summary>A value is absent that must be present.</summary>
    public const string Required = "required";

    /// <summary>A value is longer than its limit. Carries <c>max</c>.</summary>
    public const string TooLong = "too_long";

    /// <summary>A value is shorter than its limit. Carries <c>min</c>.</summary>
    public const string TooShort = "too_short";

    /// <summary>
    /// A value breaches a length constraint that bounds it at both ends. Carries <c>min</c> and <c>max</c>.
    /// </summary>
    /// <remarks>
    /// One code rather than <see cref="TooLong"/> and <see cref="TooShort"/> because a two-ended constraint
    /// reports the same message whichever end was breached, leaving nothing to tell them apart. The client has
    /// both bounds and composes the sentence itself.
    /// </remarks>
    public const string InvalidLength = "invalid_length";

    /// <summary>A number falls outside its permitted range. Carries <c>min</c> and <c>max</c>.</summary>
    public const string OutOfRange = "out_of_range";

    /// <summary>A number is zero or negative where only a positive value makes sense.</summary>
    public const string MustBePositive = "must_be_positive";

    /// <summary>A value exceeds a ceiling. Carries <c>max</c>.</summary>
    public const string ExceedsMax = "exceeds_max";

    /// <summary>A value is not a well-formed email address.</summary>
    public const string InvalidEmail = "invalid_email";

    /// <summary>A value does not match the pattern this property requires. Carries <c>pattern</c>.</summary>
    public const string InvalidFormat = "invalid_format";

    /// <summary>A value differs from the one it was required to equal. Carries <c>other</c>.</summary>
    public const string MustMatch = "must_match";

    /// <summary>A value is not one this property accepts, for a reason no more specific code covers.</summary>
    public const string Invalid = "invalid";

    /// <summary>A value is well-formed but already taken by something else.</summary>
    public const string Taken = "taken";
}

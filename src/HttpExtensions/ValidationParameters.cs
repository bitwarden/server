namespace Bit.HttpExtensions;

/// <summary>
/// The repo-wide spelling of the keys an <see cref="ErrorCode.Parameters"/> bag carries.
/// </summary>
/// <remarks>
/// A shared list for the same reason <see cref="ValidationCodes"/> is one: a client renders its own message by
/// looking a substitution up by name, so two validators reporting the same limit as <c>max</c> and <c>maximum</c>
/// break it exactly as surely as two spellings of the code would.
/// </remarks>
public static class ValidationParameters
{
    /// <summary>The lower bound a value missed.</summary>
    public const string Min = "min";

    /// <summary>The upper bound a value exceeded.</summary>
    public const string Max = "max";

    /// <summary>The pattern a value did not match.</summary>
    public const string Pattern = "pattern";

    /// <summary>The property this one was required to match.</summary>
    public const string Other = "other";
}

namespace Bit.HttpExtensions;

/// <summary>
/// One code a validated property can report.
/// </summary>
/// <param name="Message">
/// Formats the message the framework records when this constraint fails, given the property's display name. Used
/// only to tell candidates apart when a property carries more than one constraint.
/// </param>
/// <remarks>
/// A function rather than a string so the wording is asked of the constraint itself at the moment it is needed,
/// rather than copied at build time and left to drift. Exactly one candidate on a property may leave this null:
/// it is the fallback, taken when no other candidate claims the message. That is what lets a property whose other
/// constraint cannot be constructed under trimming still resolve — the one we can ask about identifies itself,
/// and the remaining failure must be the other.
/// </remarks>
public sealed record ValidationCodeCandidate(
    string Code,
    Func<string, string>? Message = null,
    IReadOnlyList<KeyValuePair<string, object?>>? Parameters = null);

/// <summary>
/// What is known about one validated property: the name it goes by on the wire, the name the framework puts in
/// its messages, and the codes it can report.
/// </summary>
/// <param name="WirePath">
/// The path as the client sent it, with <c>[]</c> where an index belongs — <c>members[].email</c>.
/// </param>
public sealed record ValidationCodeEntry(
    string WirePath,
    string DisplayName,
    IReadOnlyList<ValidationCodeCandidate> Candidates);

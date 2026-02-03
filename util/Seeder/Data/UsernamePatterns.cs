using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data;

internal sealed record UsernamePattern(
    UsernamePatternType Type,
    string FormatDescription,
    Func<string, string, string, string> Generate);

/// <summary>
/// Username pattern implementations for different email conventions.
/// </summary>
internal static class UsernamePatterns
{
    public static readonly UsernamePattern FirstDotLast = new(
        UsernamePatternType.FirstDotLast,
        "first.last@domain",
        (first, last, domain) => $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@{domain}");

    public static readonly UsernamePattern FDotLast = new(
        UsernamePatternType.FDotLast,
        "f.last@domain",
        (first, last, domain) => $"{char.ToLowerInvariant(first[0])}.{last.ToLowerInvariant()}@{domain}");

    public static readonly UsernamePattern FLast = new(
        UsernamePatternType.FLast,
        "flast@domain",
        (first, last, domain) => $"{char.ToLowerInvariant(first[0])}{last.ToLowerInvariant()}@{domain}");

    public static readonly UsernamePattern LastDotFirst = new(
        UsernamePatternType.LastDotFirst,
        "last.first@domain",
        (first, last, domain) => $"{last.ToLowerInvariant()}.{first.ToLowerInvariant()}@{domain}");

    public static readonly UsernamePattern First_Last = new(
        UsernamePatternType.First_Last,
        "first_last@domain",
        (first, last, domain) => $"{first.ToLowerInvariant()}_{last.ToLowerInvariant()}@{domain}");

    public static readonly UsernamePattern LastFirst = new(
        UsernamePatternType.LastFirst,
        "lastf@domain",
        (first, last, domain) => $"{last.ToLowerInvariant()}{char.ToLowerInvariant(first[0])}@{domain}");

    public static readonly UsernamePattern[] All = [FirstDotLast, FDotLast, FLast, LastDotFirst, First_Last, LastFirst];

    public static UsernamePattern GetPattern(UsernamePatternType type) => type switch
    {
        UsernamePatternType.FirstDotLast => FirstDotLast,
        UsernamePatternType.FDotLast => FDotLast,
        UsernamePatternType.FLast => FLast,
        UsernamePatternType.LastDotFirst => LastDotFirst,
        UsernamePatternType.First_Last => First_Last,
        UsernamePatternType.LastFirst => LastFirst,
        _ => FirstDotLast
    };
}

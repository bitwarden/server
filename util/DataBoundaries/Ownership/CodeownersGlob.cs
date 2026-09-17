using System.Text;
using System.Text.RegularExpressions;

namespace Bit.DataBoundaries.Ownership;

/// <summary>
/// Translates a CODEOWNERS pattern into a regular expression using gitignore glob semantics,
/// which is what GitHub applies when it evaluates CODEOWNERS.
/// </summary>
internal static class CodeownersGlob
{
    public static Regex ToRegex(string pattern)
    {
        var directoryOnly = pattern.EndsWith('/');
        var body = directoryOnly ? pattern[..^1] : pattern;

        // A pattern containing a slash anywhere but the end is anchored to the repository root;
        // a pattern with no slash matches a file or directory of that name at any depth.
        var anchored = body.Contains('/');
        var segments = body.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        var expression = new StringBuilder("^");
        if (!anchored)
        {
            expression.Append("(?:[^/]+/)*");
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var isLast = i == segments.Length - 1;

            if (segments[i] == "**")
            {
                expression.Append(isLast ? ".+" : "(?:[^/]+/)*");
                continue;
            }

            AppendSegment(expression, segments[i]);
            if (!isLast)
            {
                expression.Append('/');
            }
        }

        // Matching a directory transfers ownership of everything beneath it.
        expression.Append(directoryOnly ? "/.+$" : "(?:/.+)?$");

        // No IgnoreCase: GitHub evaluates CODEOWNERS patterns case-sensitively.
        return new Regex(expression.ToString(), RegexOptions.None);
    }

    private static void AppendSegment(StringBuilder expression, string segment)
    {
        foreach (var character in segment)
        {
            switch (character)
            {
                case '*':
                    expression.Append("[^/]*");
                    break;
                case '?':
                    expression.Append("[^/]");
                    break;
                default:
                    expression.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }
    }
}

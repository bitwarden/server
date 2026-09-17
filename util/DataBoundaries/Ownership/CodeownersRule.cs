using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Bit.DataBoundaries.Ownership;

internal sealed class CodeownersRule
{
    private readonly Regex _regex;

    internal CodeownersRule(int lineNumber, string pattern, ImmutableArray<string> owners)
    {
        LineNumber = lineNumber;
        Pattern = pattern;
        Owners = owners;
        _regex = CodeownersGlob.ToRegex(pattern);
    }

    public int LineNumber { get; }

    public string Pattern { get; }

    /// <summary>
    /// Empty means the rule clears ownership for every path it matches.
    /// </summary>
    public ImmutableArray<string> Owners { get; }

    public bool Matches(string repoRelativePath) => _regex.IsMatch(repoRelativePath);
}

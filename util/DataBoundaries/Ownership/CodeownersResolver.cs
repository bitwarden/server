using System.Collections.Immutable;

namespace Bit.DataBoundaries.Ownership;

internal sealed class CodeownersResolver
{
    private static readonly char[] _tokenSeparators = [' ', '\t', '\r'];

    private readonly ImmutableArray<CodeownersRule> _rules;

    private CodeownersResolver(ImmutableArray<CodeownersRule> rules) => _rules = rules;

    public static CodeownersResolver FromFile(string codeownersPath)
    {
        if (!File.Exists(codeownersPath))
        {
            throw new FileNotFoundException($"CODEOWNERS not found: {codeownersPath}", codeownersPath);
        }

        return FromLines(File.ReadAllLines(codeownersPath));
    }

    public static CodeownersResolver Parse(string content) => FromLines(content.Split('\n'));

    /// <summary>
    /// Later rules win, matching GitHub: the last matching entry decides ownership.
    /// </summary>
    public ImmutableArray<string> Resolve(string repoRelativePath) =>
        ResolveRule(repoRelativePath)?.Owners ?? [];

    /// <summary>
    /// The rule that decided ownership, or null when no pattern matches. Callers that need to justify an
    /// attribution use this to cite the winning CODEOWNERS line, since last-match-wins makes the line
    /// number the only way to see which of several matching patterns applied.
    /// </summary>
    public CodeownersRule? ResolveRule(string repoRelativePath)
    {
        CodeownersRule? winner = null;
        foreach (var rule in _rules)
        {
            if (rule.Matches(repoRelativePath))
            {
                winner = rule;
            }
        }

        return winner;
    }

    /// <summary>
    /// Patterns that match none of the supplied paths, i.e. rules that guard nothing.
    /// </summary>
    public ImmutableArray<string> FindUnmatchedPatterns(IEnumerable<string> allRepoPaths)
    {
        var paths = allRepoPaths as IReadOnlyCollection<string> ?? [.. allRepoPaths];
        return [.. _rules.Where(rule => !paths.Any(rule.Matches)).Select(rule => rule.Pattern)];
    }

    private static CodeownersResolver FromLines(IEnumerable<string> lines)
    {
        var rules = ImmutableArray.CreateBuilder<CodeownersRule>();
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;

            var line = rawLine;
            var comment = line.IndexOf('#');
            if (comment >= 0)
            {
                line = line[..comment];
            }

            var tokens = line.Split(_tokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            rules.Add(new CodeownersRule(lineNumber, tokens[0], [.. tokens.Skip(1)]));
        }

        return new CodeownersResolver(rules.ToImmutable());
    }
}

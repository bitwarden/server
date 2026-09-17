using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bit.DataBoundaries.Domains;

internal sealed class DomainResolver
{
    public const string RepoRelativePath = "util/DataBoundaries/config/domain-map.json";

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ImmutableArray<string> _segmentPrefixes;

    private readonly ImmutableArray<(Regex Pattern, string Domain)> _projectDomains;

    private readonly ImmutableDictionary<string, string> _aliases;

    private readonly ImmutableHashSet<string> _universalSurfaces;

    private readonly ImmutableHashSet<string> _knownDomains;

    private DomainResolver(DomainMap map)
    {
        _segmentPrefixes = [.. map.SegmentPrefixes];
        _aliases = map.Aliases.ToImmutableDictionary(StringComparer.Ordinal);
        _universalSurfaces = map.UniversalSurfaces.ToImmutableHashSet(StringComparer.Ordinal);

        _knownDomains = map.CanonicalDomains
            .Concat(map.Aliases.Keys)
            .Concat(map.Aliases.Values)
            .Concat(map.ProjectDomains.Values)
            .ToImmutableHashSet(StringComparer.Ordinal);

        // Longest pattern first so the match is independent of JSON key order and the more
        // specific project path wins when two prefixes overlap.
        _projectDomains =
        [
            .. map.ProjectDomains
                .OrderByDescending(entry => entry.Key.Length)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => (ToRegex(entry.Key), entry.Value))
        ];
    }

    public static DomainResolver FromMap(DomainMap map) => new(map);

    /// <summary>
    /// Reads the map committed alongside the tool. Deriving it from the repository root keeps it on the
    /// same root as the schema and CODEOWNERS inputs, so --repo-root redirects all three together.
    /// </summary>
    public static DomainResolver FromRepo(string repoRoot) =>
        FromFile(Path.Combine(repoRoot, RepoRelativePath));

    public static DomainResolver FromFile(string domainMapPath)
    {
        if (!File.Exists(domainMapPath))
        {
            throw new FileNotFoundException($"Domain map not found: {domainMapPath}", domainMapPath);
        }

        DomainMap? map;
        try
        {
            map = JsonSerializer.Deserialize<DomainMap>(File.ReadAllText(domainMapPath), _readOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Domain map is not valid JSON: {domainMapPath}", exception);
        }

        return new DomainResolver(
            map ?? throw new InvalidDataException($"Domain map is empty or not an object: {domainMapPath}"));
    }

    /// <summary>
    /// Null when no rule claims the path.
    /// </summary>
    public DomainAssignment? Resolve(string repoRelativePath)
    {
        foreach (var prefix in _segmentPrefixes)
        {
            if (!repoRelativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var remainder = repoRelativePath[prefix.Length..];
            var slash = remainder.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            if (ResolveSegment(remainder[..slash]) is { } candidate)
            {
                return candidate;
            }
        }

        foreach (var (pattern, domain) in _projectDomains)
        {
            if (pattern.IsMatch(repoRelativePath))
            {
                return Normalize(domain);
            }
        }

        return null;
    }

    /// <summary>
    /// Null unless the segment names a real domain. Without this gate, every structural folder would
    /// become a domain of its own invention.
    /// </summary>
    public DomainAssignment? ResolveSegment(string rawSegment)
    {
        var candidate = Normalize(rawSegment);
        return _knownDomains.Contains(candidate.Domain) ? candidate : null;
    }

    /// <summary>
    /// Lowercases a raw folder segment, applies alias normalization, and flags universal surfaces.
    /// </summary>
    public DomainAssignment Normalize(string rawDomain)
    {
        var lowered = rawDomain.ToLowerInvariant();
        var canonical = _aliases.TryGetValue(lowered, out var alias) ? alias : lowered;
        return new DomainAssignment(canonical, _universalSurfaces.Contains(canonical));
    }

    private static Regex ToRegex(string pattern)
    {
        var expression = new StringBuilder("^");
        foreach (var character in pattern)
        {
            expression.Append(character == '*' ? "[^/]*" : Regex.Escape(character.ToString()));
        }

        expression.Append("(?:/.+)?$");
        return new Regex(expression.ToString(), RegexOptions.None);
    }
}

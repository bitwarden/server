namespace Bit.DataBoundaries.Domains;

/// <summary>
/// Deserialized shape of config/domain-map.json.
/// </summary>
internal sealed class DomainMap
{
    /// <summary>
    /// Path prefixes whose next path segment names the domain, in evaluation order.
    /// </summary>
    public List<string> SegmentPrefixes { get; init; } = [];

    /// <summary>
    /// The product areas this map is allowed to name. A folder segment following a prefix only counts as
    /// a domain when it normalizes into this set, so structural folders such as Controllers, Views, or
    /// Utilities fall through to <see cref="ProjectDomains"/> instead of becoming invented domains.
    /// </summary>
    public List<string> CanonicalDomains { get; init; } = [];

    /// <summary>
    /// Whole-project paths that map to one domain. Keys may use <c>*</c> within a segment.
    /// </summary>
    public Dictionary<string, string> ProjectDomains { get; init; } = [];

    /// <summary>
    /// Lowercased folder spellings mapped to their canonical domain name.
    /// </summary>
    public Dictionary<string, string> Aliases { get; init; } = [];

    /// <summary>
    /// Domains whose code touches nearly every column, so their appearance carries no ownership signal:
    /// the staff portal, the org-admin API models, and the license claims factory all read broadly across
    /// the schema. They stay in the data and are filtered at presentation time only.
    /// </summary>
    public List<string> UniversalSurfaces { get; init; } = [];
}

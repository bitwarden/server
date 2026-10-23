using Bitwarden.Server.Sdk.RestrictedDependencies;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// One use of a restricted type the analyzer observed, aggregated per site.
/// </summary>
/// <param name="Project">Assembly name of the compilation the use was seen in.</param>
/// <param name="Type">Fully qualified metadata name of the restricted type.</param>
/// <param name="Kind">The access shape this use takes.</param>
/// <param name="Member">Documentation-comment id of the restricted member; null except for member uses.</param>
/// <param name="Site">Documentation-comment id of the member that contains the use.</param>
/// <param name="Count">How many uses of this shape the containing member holds.</param>
/// <param name="File">Repo-relative path of the first location, for logs only; baselines are path-independent.</param>
/// <param name="Excepted">
/// True when a valid exception covers it, so it is debt but not budget and is never written to a
/// baseline.
/// </param>
/// <param name="Tracked">
/// True when the member's rule is (AllowExistingUses, AllowNewUses) = (true, true): recorded but
/// not gated.
/// </param>
internal sealed record ObservedUsage(
    string Project,
    string Type,
    DependencyUsageType Kind,
    string? Member,
    string Site,
    int Count,
    string File,
    bool Excepted,
    bool Tracked);

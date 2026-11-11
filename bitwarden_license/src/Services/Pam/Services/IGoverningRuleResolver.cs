using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the rule that governs <paramref name="cipherId"/> for the caller, or null unless every collection they
    /// reach it through has an enabled rule. Of several, an automatic rule that admits <paramref name="signals"/> wins,
    /// then a human-approval rule, then an automatic rule that denies, then an unreadable rule; the oldest wins within
    /// each. Signals only choose among the rules, never whether one governs. The returned conditions are unevaluated.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);

    /// <summary>
    /// <see cref="ResolveAsync"/> for every cipher the caller reaches through a collection, in a fixed number of
    /// queries, keyed by cipher id. A cipher nothing governs is left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GoverningRule>> ResolveManyAsync(Guid userId, AccessSignals signals);

    /// <summary>
    /// Loads the rule a request pinned at submit instead of re-resolving, so a rule created or re-pointed since cannot
    /// take over. Null if the rule is disabled or deleted.
    /// </summary>
    Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId);
}

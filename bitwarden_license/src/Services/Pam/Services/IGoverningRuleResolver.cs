using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the access rule that governs <paramref name="cipherId"/> for the caller, or null if not
    /// leasing-gated: every collection they can reach it through must carry an enabled rule, and one that does
    /// not is an escape. Oldest rule wins (earliest creation date, ties broken on rule id), structurally, whatever
    /// a newer path would evaluate to for <paramref name="signals"/>. Conditions are returned unevaluated.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);

    /// <summary>
    /// Loads the rule a request pinned at submit (<c>AccessRequest.RuleId</c>) rather than re-deriving it, and so
    /// does not look for an escape path. Use it for any operation on an existing request: re-resolving would let a
    /// rule created or re-pointed since submit silently take over.
    /// </summary>
    /// <param name="ruleId">The pinned rule.</param>
    /// <param name="collectionId">The collection the request was made through, carried on the request.</param>
    /// <returns>The pinned rule, or null if it no longer gates access (disabled or deleted).</returns>
    Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId);
}

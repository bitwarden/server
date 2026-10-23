using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the rule that governs <paramref name="cipherId"/> for the caller, or null unless every collection they
    /// reach it through has an enabled rule. The oldest rule wins regardless of <paramref name="signals"/>; conditions
    /// are returned unevaluated.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);

    /// <summary>
    /// Loads the rule a request pinned at submit instead of re-resolving, so a rule created or re-pointed since cannot
    /// take over. Null if the rule is disabled or deleted.
    /// </summary>
    Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId);
}

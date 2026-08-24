using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the access rule that governs <paramref name="cipherId"/> for the caller, or null when the cipher is
    /// not leasing-gated for them (no reachable collection carries an access rule). When more than one governing
    /// collection applies, the oldest rule wins — the one with the earliest creation date, ties broken on rule id so
    /// the choice is total and stable. Selection is purely structural and does NOT depend on how a rule's conditions
    /// evaluate for the current <paramref name="signals"/>: a newer path never pre-empts an older one, whichever is
    /// the more permissive, so a caller may be routed to an approver even though a newer path would have auto-granted.
    /// Whether the resolved rule requires human approval is structural in the same way — it is carried by a
    /// human-approval condition on the rule rather than derived from a verdict — so it does not vary with
    /// <paramref name="signals"/> either. The rule's conditions are returned unevaluated for the caller to apply.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);

    /// <summary>
    /// Loads the rule a request pinned at submit (<c>AccessRequest.RuleId</c>), rather than re-deriving which rule
    /// governs the caller now. Use this from any operation that acts on an existing request: re-resolving would apply
    /// oldest-wins over today's collections and rules, so a rule created or re-pointed since submit could silently
    /// take over from the one that actually decided the request.
    /// </summary>
    /// <param name="ruleId">The pinned rule.</param>
    /// <param name="collectionId">
    /// The collection the request was made through, carried on the request. Supplied rather than re-derived because it
    /// is a fact about the request, not about which collections reach the cipher today.
    /// </param>
    /// <returns>
    /// The pinned rule, or null when it no longer gates access — it was disabled, or deleted (a delete clears the pin,
    /// so in practice this is the narrow window before that lands). Null means ungated, exactly as it does for
    /// <see cref="ResolveAsync"/>: there is no rule left to hold the caller to.
    /// </returns>
    Task<GoverningRule?> ResolvePinnedAsync(Guid ruleId, Guid collectionId);
}

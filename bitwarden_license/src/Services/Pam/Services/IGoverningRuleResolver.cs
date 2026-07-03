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
    /// The resolved rule's conditions are still evaluated against <paramref name="signals"/> to report whether it
    /// requires human approval.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);
}

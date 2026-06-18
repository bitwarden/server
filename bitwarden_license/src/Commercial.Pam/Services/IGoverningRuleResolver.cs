using Bit.Commercial.Pam.Engine;
using Bit.Commercial.Pam.Models;

namespace Bit.Commercial.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the leasing context that governs <paramref name="cipherId"/> for the caller, evaluating each
    /// reachable collection's rule against the request <paramref name="signals"/>, or null when the cipher is not
    /// leasing-gated for them (no reachable collection carries an access rule). When more than one governing
    /// collection applies, the least-restrictive applicable rule wins: an automatic grant is favoured over one that
    /// needs human approval, which is favoured over a denial — so the caller is never routed to an approver when some
    /// path would auto-grant, and a failing automatic rule never pre-empts a path that would grant.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId, AccessSignals signals);
}

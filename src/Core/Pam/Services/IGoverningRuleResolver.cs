using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.Services;

public interface IGoverningRuleResolver
{
    /// <summary>
    /// Resolves the leasing context that governs <paramref name="cipherId"/> for the caller, or null when the cipher
    /// is not leasing-gated for them (no reachable collection carries an access rule). When more than one governing
    /// collection applies, the most restrictive (human-approval) one wins.
    /// </summary>
    Task<GoverningRule?> ResolveAsync(Guid userId, Guid cipherId);
}

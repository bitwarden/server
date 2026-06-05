using Bit.Core.Pam.Models.Rules;

namespace Bit.Core.Pam.Engine;

/// <summary>
/// Evaluates the structured access <see cref="Rule"/> that governs a cipher against the request-time
/// <see cref="AccessPolicySignals"/>, deciding whether access is allowed, denied, or gated on human approval.
/// The engine is pure: it reads no state and issues no leases. Lease lifecycle is owned by the lease commands and
/// queries, which call the engine to decide whether a lease may be issued or its data handed over.
/// </summary>
public interface IAccessPolicyEngine
{
    AccessDecision Evaluate(Rule rule, AccessPolicySignals signals);
}

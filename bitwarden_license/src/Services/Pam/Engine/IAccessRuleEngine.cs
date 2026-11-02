using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Evaluates an access rule's conditions — a flat list of <see cref="AccessCondition"/> ANDed together — against
/// the request-time <see cref="AccessSignals"/>, deciding whether access is allowed, denied, or gated on human
/// approval. The engine is pure: it reads no state and issues no leases. Lease lifecycle is owned by the lease
/// commands and queries, which call the engine to decide whether a lease may be issued or its data handed over.
/// </summary>
public interface IAccessRuleEngine
{
    AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals);
}

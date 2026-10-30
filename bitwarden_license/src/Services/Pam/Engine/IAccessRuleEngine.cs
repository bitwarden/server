using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Evaluates an access rule's <see cref="AccessCondition"/>s, ANDed together, against the request-time
/// <see cref="AccessSignals"/>. Pure: it reads no state and issues no leases.
/// </summary>
public interface IAccessRuleEngine
{
    AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals);
}

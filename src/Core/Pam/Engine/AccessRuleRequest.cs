using System.Diagnostics.CodeAnalysis;

namespace Bit.Core.Pam.Engine;

public sealed class AccessRuleRequest
{
    public required Guid CipherId { get; init; }
    public required string Username { get; init; }
    public required bool Approved { get; init; }

    /// <summary>
    /// The signals captured when access was requested. The lease exchange re-evaluates the rule
    /// against these, so the requester cannot alter their context between requesting and exchanging.
    /// </summary>
    public required AccessRuleSignals Signals { get; init; }
}

public interface IAccessRuleRequestRepository
{
    AccessRuleRequest Create(Guid cipherId, AccessRuleSignals signals);
    bool Delete(AccessRuleRequest request);
    bool TryGet(Guid cipherId, string username, [NotNullWhen(true)] out AccessRuleRequest? request);
}

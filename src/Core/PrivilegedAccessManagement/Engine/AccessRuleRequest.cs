using System.Diagnostics.CodeAnalysis;

namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleRequest
{
    public required Guid CipherId { get; init; }
    public required string Username { get; init; }
    public required bool Approved { get; init; }
}

public interface IAccessRuleRequestRepository
{
    AccessRuleRequest Create(Guid cipherId, string username);
    bool Delete(AccessRuleRequest request);
    bool TryGet(Guid cipherId, string username, [NotNullWhen(true)] out AccessRuleRequest? request);
}

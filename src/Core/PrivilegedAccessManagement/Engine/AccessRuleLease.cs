using System.Diagnostics.CodeAnalysis;

namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleLease
{
    public required Guid CipherId { get; init; }
    public required string Username { get; init; }
    public required DateTime Expires { get; init; }
}

public interface IAccessRuleLeaseRepository
{
    bool TryCreate(AccessRuleRequest request, TimeSpan duration, [NotNullWhen(true)] out AccessRuleLease? lease);
    bool TryGet(Guid cipherId, string username, [NotNullWhen(true)] out AccessRuleLease? lease);
    bool TryGet(Guid cipherId, [NotNullWhen(true)] out AccessRuleLease? lease);
}

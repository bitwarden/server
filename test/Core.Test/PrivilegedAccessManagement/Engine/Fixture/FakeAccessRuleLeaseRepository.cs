using Bit.Core.PrivilegedAccessManagement.Engine;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine;

public sealed class FakeAccessRuleLeaseRepository : IAccessRuleLeaseRepository
{
    private readonly TimeProvider _time;
    private readonly List<AccessRuleLease> _leases = [];

    public FakeAccessRuleLeaseRepository(TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public bool FailCreate { get; set; }
    public int CreatedCount { get; private set; }

    public void Seed(AccessRuleLease lease)
    {
        _leases.Add(lease);
    }

    public bool TryCreate(AccessRuleRequest request, TimeSpan duration, out AccessRuleLease? lease)
    {
        if (FailCreate)
        {
            lease = null;
            return false;
        }

        lease = new AccessRuleLease
        {
            CipherId = request.CipherId,
            Username = request.Username,
            Expires = _time.GetUtcNow().UtcDateTime.Add(duration),
        };
        _leases.Add(lease);
        CreatedCount++;
        return true;
    }

    public bool TryGet(Guid cipherId, string username, out AccessRuleLease? lease)
    {
        lease = _leases.FirstOrDefault(l => l.CipherId == cipherId && l.Username == username);
        return lease is not null;
    }

    public bool TryGet(Guid cipherId, out AccessRuleLease? lease)
    {
        lease = _leases.FirstOrDefault(l => l.CipherId == cipherId);
        return lease is not null;
    }
}

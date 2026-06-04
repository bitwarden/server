using Bit.Core.Pam.Engine;

namespace Bit.Core.Test.Pam.Engine;

public sealed class FakeAccessRuleRequestRepository : IAccessRuleRequestRepository
{
    private readonly List<AccessRuleRequest> _requests = [];

    public int CreatedCount { get; private set; }

    public void Seed(AccessRuleRequest request) => _requests.Add(request);

    public AccessRuleRequest Create(Guid cipherId, AccessRuleSignals signals)
    {
        var request = new AccessRuleRequest
        {
            CipherId = cipherId,
            Username = signals.Username,
            Approved = false,
            Signals = signals,
        };
        _requests.Add(request);
        CreatedCount++;
        return request;
    }

    public void Approve(Guid cipherId, string username)
    {
        var existing = _requests.FirstOrDefault(r => r.CipherId == cipherId && r.Username == username);
        if (existing is null)
        {
            return;
        }

        _requests.Remove(existing);
        _requests.Add(new AccessRuleRequest
        {
            CipherId = existing.CipherId,
            Username = existing.Username,
            Approved = true,
            Signals = existing.Signals,
        });
    }

    public bool Delete(AccessRuleRequest request) => _requests.Remove(request);

    public bool TryGet(Guid cipherId, string username, out AccessRuleRequest? request)
    {
        request = _requests.FirstOrDefault(r => r.CipherId == cipherId && r.Username == username);
        return request is not null;
    }
}

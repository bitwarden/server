using System.Collections.Concurrent;

namespace Bit.Core.Services;

public class PhishingDomainService : IPhishingDomainService
{
    private readonly ConcurrentDictionary<string, byte> _phishingDomains = new();

    public Task<IEnumerable<string>> GetPhishingDomainsAsync()
    {
        return Task.FromResult(_phishingDomains.Keys.AsEnumerable());
    }
}

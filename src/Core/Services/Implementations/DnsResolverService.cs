using Bit.Core.Exceptions;
using DnsClient;

namespace Bit.Core.Services;

public class DnsResolverService : IDnsResolverService
{
    private readonly LookupClient _lookupClient;

    public DnsResolverService(LookupClient lookupClient)
    {
        _lookupClient = lookupClient;
    }
    public async Task<bool> ResolveAsync(string domain, string txtRecord, CancellationToken cancellationToken = default)
    {
        var result = await _lookupClient.QueryAsync(new DnsQuestion(domain, QueryType.TXT), cancellationToken);
        if (!result.HasError)
        {
            return result.Answers.TxtRecords()
                .Select(t => t?.EscapedText?.FirstOrDefault())
                .Any(t => t == txtRecord);
        }

        throw new DnsQueryException(result.ErrorMessage);
    }
}

using Bit.Core.Exceptions;
using DnsClient;

namespace Bit.Core.Services;

public class DnsResolverService : IDnsResolverService
{
    public async Task<bool> ResolveAsync(string domain, string txtRecord, CancellationToken cancellationToken = default)
    {
        var lookup = new LookupClient();
        var result = await lookup.QueryAsync(new DnsQuestion(domain, QueryType.TXT), cancellationToken);
        if (!result.HasError)
        {
            return result.Answers.TxtRecords()
                .Select(t => t?.EscapedText?.FirstOrDefault())
                .Any(t => t == txtRecord);
        }

        throw new DnsQueryException(result.ErrorMessage);
    }
}

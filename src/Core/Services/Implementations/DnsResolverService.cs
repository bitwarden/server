using Bit.Core.Exceptions;
using DnsClient;

namespace Bit.Core.Services;

public class DnsResolverService : IDnsResolverService
{
    public async Task<bool> ResolveAsync(string domain, string txtRecord, CancellationToken cancellationToken = default)
    {
        //increased timeout to 15 seconds to avoid timeouts
        var options = new LookupClientOptions {Timeout = TimeSpan.FromSeconds(15)};
        var lookup = new LookupClient(options);
        
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

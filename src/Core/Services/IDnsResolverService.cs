namespace Bit.Core.Services;

public interface IDnsResolverService
{
    Task<bool> ResolveAsync(string domain, string txtRecord, CancellationToken cancellationToken = default);
}

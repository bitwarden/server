namespace Bit.Core.Services;

public interface IPhishingDomainService
{
    Task<IEnumerable<string>> GetPhishingDomainsAsync();
}

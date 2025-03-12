namespace Bit.Core.Repositories;

public interface IPhishingDomainRepository
{
    Task<ICollection<string>> GetActivePhishingDomainsAsync();
    Task UpdatePhishingDomainsAsync(IEnumerable<string> domains);
}

namespace Bit.Core.PhishingDomainFeatures.Interfaces;

public interface ICloudPhishingDomainQuery
{
    Task<List<string>> GetPhishingDomainsAsync();
    Task<string> GetRemoteChecksumAsync();
}

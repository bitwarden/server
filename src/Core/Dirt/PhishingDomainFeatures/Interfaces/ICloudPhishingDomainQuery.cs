namespace Bit.Core.Dirt.PhishingDomainFeatures.Interfaces;

public interface ICloudPhishingDomainQuery
{
    Task<List<string>> GetPhishingDomainsAsync();
    Task<string> GetRemoteChecksumAsync();
}

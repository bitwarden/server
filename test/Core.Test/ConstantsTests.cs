using Xunit;

namespace Bit.Core.Test;

public class ConstantsTests
{
    [Theory]
    [InlineData("bitwarden.com")]
    [InlineData("bitwarden.eu")]
    [InlineData("bitwarden-gov.com")]
    public void BitwardenCloudDomains_ContainsAllProductionDomains(string domain)
    {
        Assert.Contains(domain, Constants.BitwardenCloudDomains);
    }

    [Theory]
    [InlineData("https://bitwarden.com/sso-callback")]
    [InlineData("https://bitwarden.eu/sso-callback")]
    [InlineData("https://bitwarden-gov.com/sso-callback")]
    public void BitwardenMobileSsoCallbackUris_ContainsAllRegionCallbacks(string uri)
    {
        Assert.Contains(uri, Constants.BitwardenMobileSsoCallbackUris);
    }
}

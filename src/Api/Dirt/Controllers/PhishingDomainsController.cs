using Bit.Core;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("phishing-domains")]
public class PhishingDomainsController(IPhishingDomainRepository phishingDomainRepository, IFeatureService featureService) : Controller
{
    [HttpGet]
    public async Task<ActionResult<ICollection<string>>> GetPhishingDomainsAsync()
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PhishingDetection))
        {
            return NotFound();
        }

        var domains = await phishingDomainRepository.GetActivePhishingDomainsAsync();
        return Ok(domains);
    }

    [HttpGet("checksum")]
    public async Task<ActionResult<string>> GetChecksumAsync()
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.PhishingDetection))
        {
            return NotFound();
        }

        var checksum = await phishingDomainRepository.GetCurrentChecksumAsync();
        return Ok(checksum);
    }
}

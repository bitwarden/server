using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("phishing-domains")]
[Authorize("PhishingDomains")]
public class PhishingDomainsController(IPhishingDomainRepository phishingDomainRepository) : Controller
{
    [HttpGet]
    public async Task<ActionResult<ICollection<string>>> GetPhishingDomainsAsync()
    {
        var domains = await phishingDomainRepository.GetActivePhishingDomainsAsync();
        return Ok(domains);
    }

    [HttpGet("checksum")]
    public async Task<ActionResult<string>> GetChecksumAsync()
    {
        var checksum = await phishingDomainRepository.GetCurrentChecksumAsync();
        return Ok(checksum);
    }
}

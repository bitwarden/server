using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconsController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly IMemoryCache _memoryCache;
        private readonly IDomainMappingService _domainMappingService;
        private readonly IconsSettings _iconsSettings;

        public IconsController(
            IMemoryCache memoryCache,
            IDomainMappingService domainMappingService,
            IOptions<IconsSettings> iconsSettingsOptions)
        {
            _memoryCache = memoryCache;
            _domainMappingService = domainMappingService;
            _iconsSettings = iconsSettingsOptions.Value;
        }

        [HttpGet("")]
        public async Task<IActionResult> Get([FromQuery] string domain)
        {
            if(!domain.StartsWith("http://") || !domain.StartsWith("https://"))
            {
                domain = "http://" + domain;
            }

            if(!Uri.TryCreate(domain, UriKind.Absolute, out Uri uri))
            {
                return new BadRequestResult();
            }

            var mappedDomain = _domainMappingService.MapDomain(uri.Host);
            var icon = await _memoryCache.GetOrCreateAsync(mappedDomain, async entry =>
            {
                entry.AbsoluteExpiration = DateTime.UtcNow.AddHours(_iconsSettings.CacheHours);

                var iconUrl = $"{_iconsSettings.BestIconBaseUrl}/icon?url={mappedDomain}&size=16..24..200";
                var response = await _httpClient.GetAsync(iconUrl);
                if(!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return new Icon
                {
                    Image = await response.Content.ReadAsByteArrayAsync(),
                    Format = response.Content.Headers.ContentType.MediaType
                };
            });

            if(icon == null)
            {
                return new NotFoundResult();
            }

            return new FileContentResult(icon.Image, icon.Format);
        }
    }
}

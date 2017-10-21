using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
            IconsSettings iconsSettings)
        {
            _memoryCache = memoryCache;
            _domainMappingService = domainMappingService;
            _iconsSettings = iconsSettings;
        }

        [HttpGet("{hostname}/icon.png")]
        [ResponseCache(Duration = 86400 /*24 hours*/)]
        public async Task<IActionResult> Get(string hostname)
        {
            if(string.IsNullOrWhiteSpace(hostname) || !hostname.Contains("."))
            {
                return new BadRequestResult();
            }

            var url = $"http://{hostname}";
            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return new BadRequestResult();
            }

            var mappedDomain = _domainMappingService.MapDomain(uri.Host);
            if(!_memoryCache.TryGetValue(mappedDomain, out Icon icon))
            {
                var iconUrl = $"{_iconsSettings.BestIconBaseUrl}/icon?url={mappedDomain}&size=16..24..32" +
                    $"&fallback_icon_url=https://raw.githubusercontent.com/bitwarden/web/master/src/images/fa-globe.png";
                var response = await _httpClient.GetAsync(iconUrl);
                if(!response.IsSuccessStatusCode)
                {
                    return new NotFoundResult();
                }

                var image = await response.Content.ReadAsByteArrayAsync();
                icon = new Icon
                {
                    Image = image,
                    Format = response.Content.Headers.ContentType.MediaType
                };

                // Only cache smaller images (<= 50kb)
                if(image.Length <= 50012)
                {
                    _memoryCache.Set(mappedDomain, icon, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = new TimeSpan(_iconsSettings.CacheHours, 0, 0)
                    });
                }
            }

            return new FileContentResult(icon.Image, icon.Format);
        }
    }
}

using System;
using System.Threading.Tasks;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Bit.Icons.Controllers
{
    [Route("")]
    public class IconsController : Controller
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDomainMappingService _domainMappingService;
        private readonly IIconFetchingService _iconFetchingService;
        private readonly ILogger<IconsController> _logger;
        private readonly IconsSettings _iconsSettings;

        public IconsController(
            IMemoryCache memoryCache,
            IDomainMappingService domainMappingService,
            IIconFetchingService iconFetchingService,
            ILogger<IconsController> logger,
            IconsSettings iconsSettings)
        {
            _memoryCache = memoryCache;
            _domainMappingService = domainMappingService;
            _iconFetchingService = iconFetchingService;
            _logger = logger;
            _iconsSettings = iconsSettings;
        }

        [HttpGet("{hostname}/icon.png")]
        public async Task<IActionResult> Get(string hostname)
        {
            if(string.IsNullOrWhiteSpace(hostname) || !hostname.Contains("."))
            {
                return new BadRequestResult();
            }

            var url = $"http://{hostname}";
            if(!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new BadRequestResult();
            }

            var domain = uri.Host;
            // Convert sub.domain.com => domain.com
            //if(DomainName.TryParseBaseDomain(domain, out var baseDomain))
            //{
            //    domain = baseDomain;
            //}

            var mappedDomain = _domainMappingService.MapDomain(domain);
            if(!_iconsSettings.CacheEnabled || !_memoryCache.TryGetValue(mappedDomain, out Icon icon))
            {
                var result = await _iconFetchingService.GetIconAsync(domain);
                if(result == null)
                {
                    _logger.LogWarning("Null result returned for {0}.", domain);
                    icon = null;
                }
                else
                {
                    icon = result.Icon;
                }

                // Only cache not found and smaller images (<= 50kb)
                if(_iconsSettings.CacheEnabled && (icon == null || icon.Image.Length <= 50012))
                {
                    _logger.LogWarning("Cache the icon for {0}.", domain);
                    _memoryCache.Set(mappedDomain, icon, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = new TimeSpan(_iconsSettings.CacheHours, 0, 0),
                        Size = icon?.Image.Length ?? 0,
                        Priority = icon == null ? CacheItemPriority.High : CacheItemPriority.Normal
                    });
                }
            }

            if(icon == null)
            {
                return new NotFoundResult();
            }

            return new FileContentResult(icon.Image, icon.Format);
        }
    }
}

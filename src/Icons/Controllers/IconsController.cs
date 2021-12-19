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
        // Basic fa-globe icon
        private static readonly byte[] _notFoundImage = Convert.FromBase64String("iVBORw0KGgoAAAANSUhE" +
            "UgAAABEAAAARCAQAAACRZI9xAAABH0lEQVQoz2WQv0sDQRCF351C4g9QkXCgIkJAEREExUpJbyFoK6nSRLT/GgtLOxE" +
            "sgoX/QLAL2Alin0YkICJicQhHCrEIEsJYZLnsxVfNzH779s1KToTsUqNFzAOnRBoWyyS0+aSO0cEwrhnxgUOMMjOMk2" +
            "eVOwzDeCE/cDDWXD2B0aFAnR7GPUE/Q8KJ5zjHDYHEFr8YB5IoYbymlpIIJaap0sVICMQthpEbil9xeYxIxBjGQgY4S" +
            "wFjW27FD6bccUCBbw8piWdXNliRuKRLzQOMdXGeNj+E7FDOAMakKHptk30iHr3JU//1RuZWiysWWXKRi30kT5KBviSJ" +
            "MWKOB0vO8uYhVUJKtCH7VaNcpEhMj3c29F/k2OSICnvM+/M/XGfnuYOrfEAAAAAASUVORK5CYII=");

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

        [HttpGet("~/config")]
        public IActionResult GetConfig()
        {
            return new JsonResult(new
            {
                CacheEnabled = _iconsSettings.CacheEnabled,
                CacheHours = _iconsSettings.CacheHours,
                CacheSizeLimit = _iconsSettings.CacheSizeLimit
            });
        }

        [HttpGet("{hostname}/icon.png")]
        public async Task<IActionResult> Get(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname) || !hostname.Contains("."))
            {
                return new BadRequestResult();
            }

            var url = $"http://{hostname}";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
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
            if (!_iconsSettings.CacheEnabled || !_memoryCache.TryGetValue(mappedDomain, out Icon icon))
            {
                var result = await _iconFetchingService.GetIconAsync(domain);
                if (result == null)
                {
                    _logger.LogWarning("Null result returned for {0}.", domain);
                    icon = null;
                }
                else
                {
                    icon = result.Icon;
                }

                // Only cache not found and smaller images (<= 50kb)
                if (_iconsSettings.CacheEnabled && (icon == null || icon.Image.Length <= 50012))
                {
                    _logger.LogInformation("Cache icon for {0}.", domain);
                    _memoryCache.Set(mappedDomain, icon, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = new TimeSpan(_iconsSettings.CacheHours, 0, 0),
                        Size = icon?.Image.Length ?? 0,
                        Priority = icon == null ? CacheItemPriority.High : CacheItemPriority.Normal
                    });
                }
            }

            if (icon == null)
            {
                return new FileContentResult(_notFoundImage, "image/png");
            }

            return new FileContentResult(icon.Image, icon.Format);
        }
    }
}

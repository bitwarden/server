using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Icons.Controllers;

[Route("")]
public class IconsController : Controller
{
    // Basic bwi-globe icon
    private static readonly byte[] _notFoundImage = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUg" +
        "AAABMAAAATCAQAAADYWf5HAAABu0lEQVR42nXSvWuTURTH8R+t0heI9Y04aJycdBLNJNrBFBU7OFgUER3q21I0bXK+JwZ" +
        "pXISm/QdcRB3EgqBBsNihsUbbgODQQSKCuKSDOApJuuhj8tCYQj/jvYfD795z1MZ+nBKrNKhSwrMxbZTrtRnqlEjZkB/x" +
        "C/xmhZrlc71qS0Up8yVzTCGucFNKD1JhORVd70SZNU4okNx5d4+U2UXRIpJFWLClsR79YzN88wQvLWNzzPKEeS/wkQGpW" +
        "VhhqhW8TtDJD3Mm1x/23zLSrZCdpBY8BueTNjHSbc+8wC9HlHgU5Aj5AW5zPdcVdpq0UcknWBSr/pjixO4gfp899Kd23p" +
        "M2qQCH7LkCnqAqGh73OK/8NPOcaibr90LrW/yWAnaUhqjaOSl9nFR2r5rsqo22ypn1B5IN8VOUMHVgOnNQIX+d62plcz6" +
        "rg1/jskK8CMb4we4pG6OWHtR/LBJkC2E4a7ZPkuX5ntumAOM2xxveclEhLvGH6XCmLPs735Eetrw63NnOgr9P9q1viC3x" +
        "lRUGOjImqFDuOBvrYYoaZU9z1uPpYae5NfdvbNVG2ZjDIlXq/oMi46lo++4vjjPBl2Dlg00AAAAASUVORK5CYII=");

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

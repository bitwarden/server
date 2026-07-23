using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;

namespace Bit.Icons.Controllers;

[Route("~/change-password-uri")]
public class ChangePasswordUriController : Controller
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDomainMappingService _domainMappingService;
    private readonly IChangePasswordUriService _changePasswordService;
    private readonly ChangePasswordUriSettings _changePasswordSettings;
    private readonly ILogger<ChangePasswordUriController> _logger;

    public ChangePasswordUriController(
        IMemoryCache memoryCache,
        IDomainMappingService domainMappingService,
        IChangePasswordUriService changePasswordService,
        ChangePasswordUriSettings changePasswordUriSettings,
        ILogger<ChangePasswordUriController> logger)
    {
        _memoryCache = memoryCache;
        _domainMappingService = domainMappingService;
        _changePasswordService = changePasswordService;
        _changePasswordSettings = changePasswordUriSettings;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return new JsonResult(new
        {
            _changePasswordSettings.CacheEnabled,
            _changePasswordSettings.CacheHours,
            _changePasswordSettings.CacheSizeLimit
        });
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return new BadRequestResult();
        }

        var uriHasProtocol = uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        var url = uriHasProtocol ? uri : $"https://{uri}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var validUri))
        {
            return new BadRequestResult();
        }

        var domain = validUri.Host;

        var mappedDomain = _domainMappingService.MapDomain(domain);
        if (_changePasswordSettings.CacheEnabled && _memoryCache.TryGetValue(mappedDomain, out string? cachedUri))
        {
            SetCacheControl(definitive: true);
            return Ok(new ChangePasswordUriResponse(cachedUri));
        }

        var result = await _changePasswordService.GetChangePasswordUri(domain);

        // Transient failure: don't cache, and set no-store so the edge doesn't pin a
        // "no change-password URL" answer for every client behind that cache.
        if (result.Type == ChangePasswordUriResultType.LookupFailed)
        {
            _logger.LogWarning("Change-password lookup failed for {Domain}; not caching.", domain);
            SetCacheControl(definitive: false);
            return Ok(new ChangePasswordUriResponse(null));
        }

        if (_changePasswordSettings.CacheEnabled)
        {
            _memoryCache.Set(mappedDomain, result.Uri, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = new TimeSpan(_changePasswordSettings.CacheHours, 0, 0),
                Size = result.Uri?.Length ?? 0,
                Priority = result.Uri == null ? CacheItemPriority.High : CacheItemPriority.Normal
            });
        }

        SetCacheControl(definitive: true);
        return Ok(new ChangePasswordUriResponse(result.Uri));
    }

    /// <summary>
    /// Sets Cache-Control for this endpoint: a short window for definitive answers, no-store for
    /// transient failures. Overrides the long-lived header the Icons pipeline applies to static assets.
    /// </summary>
    private void SetCacheControl(bool definitive)
    {
        Response.GetTypedHeaders().CacheControl = definitive
            ? new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromHours(1) }
            : new CacheControlHeaderValue { NoStore = true };
    }
}

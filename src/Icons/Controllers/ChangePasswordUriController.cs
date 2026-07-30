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
        if (_changePasswordSettings.CacheEnabled &&
            _memoryCache.TryGetValue(mappedDomain, out ChangePasswordUriResult? cached) && cached != null)
        {
            return BuildResponse(cached);
        }

        var result = await _changePasswordService.GetChangePasswordUri(domain);

        if (result.Type == ChangePasswordUriResultType.LookupFailed)
        {
            _logger.LogDebug("Change-password lookup for {Domain} failed; caching briefly.", domain);
        }

        if (_changePasswordSettings.CacheEnabled)
        {
            var isFailure = result.Type == ChangePasswordUriResultType.LookupFailed;
            _memoryCache.Set(mappedDomain, result, new MemoryCacheEntryOptions
            {
                // Cache a transient failure only briefly — long enough to bound the outbound probe
                // rate for a persistently-failing domain, short enough to recover quickly. Definitive
                // answers use the configured window.
                AbsoluteExpirationRelativeToNow = isFailure
                    ? TimeSpan.FromMinutes(1)
                    : new TimeSpan(_changePasswordSettings.CacheHours, 0, 0),
                Size = result.Uri?.Length ?? 0,
                Priority = isFailure ? CacheItemPriority.Low
                    : result.Uri == null ? CacheItemPriority.High
                    : CacheItemPriority.Normal
            });
        }

        return BuildResponse(result);
    }

    private IActionResult BuildResponse(ChangePasswordUriResult result)
    {
        // A failed lookup must never be stored by the edge — even when served from the origin's
        // short negative cache — so only definitive answers get an edge cache window.
        SetCacheControl(definitive: result.Type != ChangePasswordUriResultType.LookupFailed);
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

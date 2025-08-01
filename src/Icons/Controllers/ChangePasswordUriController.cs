using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Icons.Controllers;

[Route("change-password-uri")]
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
        if (!_changePasswordSettings.CacheEnabled || !_memoryCache.TryGetValue(mappedDomain, out string? changePasswordUri))
        {
            var result = await _changePasswordService.GetChangePasswordUri(domain);
            if (result == null)
            {
                _logger.LogWarning("Null result returned for {0}.", domain);
                changePasswordUri = null;
            }
            else
            {
                changePasswordUri = result;
            }

            if (_changePasswordSettings.CacheEnabled)
            {
                _logger.LogInformation("Cache uri for {0}.", domain);
                _memoryCache.Set(mappedDomain, changePasswordUri, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = new TimeSpan(_changePasswordSettings.CacheHours, 0, 0),
                    Size = changePasswordUri?.Length ?? 0,
                    Priority = changePasswordUri == null ? CacheItemPriority.High : CacheItemPriority.Normal
                });
            }
        }

        return Ok(new ChangePasswordUriResponse(changePasswordUri));
    }
}

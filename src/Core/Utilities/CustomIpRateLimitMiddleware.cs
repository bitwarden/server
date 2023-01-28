using AspNetCoreRateLimit;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Utilities;

public class CustomIpRateLimitMiddleware : IpRateLimitMiddleware
{
    private readonly IBlockIpService _blockIpService;
    private readonly ILogger<CustomIpRateLimitMiddleware> _logger;
    private readonly IDistributedCache _distributedCache;
    private readonly IpRateLimitOptions _options;

    public CustomIpRateLimitMiddleware(
        IDistributedCache distributedCache,
        IBlockIpService blockIpService,
        RequestDelegate next,
        IProcessingStrategy processingStrategy,
        IRateLimitConfiguration rateLimitConfiguration,
        IOptions<IpRateLimitOptions> options,
        IIpPolicyStore policyStore,
        ILogger<CustomIpRateLimitMiddleware> logger)
        : base(next, processingStrategy, options, policyStore, rateLimitConfiguration, logger)
    {
        _distributedCache = distributedCache;
        _blockIpService = blockIpService;
        _options = options.Value;
        _logger = logger;
    }

    public override Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter)
    {
        var message = string.IsNullOrWhiteSpace(_options.QuotaExceededMessage)
            ? $"Slow down! Too many requests. Try again in {rule.Period}."
            : _options.QuotaExceededMessage;
        httpContext.Response.Headers["Retry-After"] = retryAfter;
        httpContext.Response.StatusCode = _options.HttpStatusCode;
        var errorModel = new ErrorResponseModel { Message = message };
        return httpContext.Response.WriteAsJsonAsync(errorModel, httpContext.RequestAborted);
    }

    protected override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity,
        RateLimitCounter counter, RateLimitRule rule)
    {
        base.LogBlockedRequest(httpContext, identity, counter, rule);
        var key = $"blockedIp_{identity.ClientIp}";

        _distributedCache.TryGetValue(key, out int blockedCount);

        blockedCount++;
        if (blockedCount > 10)
        {
            _blockIpService.BlockIpAsync(identity.ClientIp, false);
            _logger.LogInformation(Constants.BypassFiltersEventId, null,
                "Banned {0}. \nInfo: \n{1}", identity.ClientIp, GetRequestInfo(httpContext));
        }
        else
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, null,
                "Request blocked {0}. \nInfo: \n{1}", identity.ClientIp, GetRequestInfo(httpContext));
            _distributedCache.Set(key, blockedCount,
                new DistributedCacheEntryOptions().SetSlidingExpiration(new TimeSpan(0, 5, 0)));
        }
    }

    private string GetRequestInfo(HttpContext httpContext)
    {
        if (httpContext == null || httpContext.Request == null)
        {
            return null;
        }

        var s = string.Empty;
        foreach (var header in httpContext.Request.Headers)
        {
            s += $"Header \"{header.Key}\": {header.Value} \n";
        }

        foreach (var query in httpContext.Request.Query)
        {
            s += $"Query \"{query.Key}\": {query.Value} \n";
        }

        return s;
    }
}

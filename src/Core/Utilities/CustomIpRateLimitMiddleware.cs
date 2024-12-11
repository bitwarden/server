using AspNetCoreRateLimit;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Utilities;

public class CustomIpRateLimitMiddleware : IpRateLimitMiddleware
{
    private readonly IpRateLimitOptions _options;

    public CustomIpRateLimitMiddleware(
        RequestDelegate next,
        IProcessingStrategy processingStrategy,
        IRateLimitConfiguration rateLimitConfiguration,
        IOptions<IpRateLimitOptions> options,
        IIpPolicyStore policyStore,
        ILogger<CustomIpRateLimitMiddleware> logger
    )
        : base(next, processingStrategy, options, policyStore, rateLimitConfiguration, logger)
    {
        _options = options.Value;
    }

    public override Task ReturnQuotaExceededResponse(
        HttpContext httpContext,
        RateLimitRule rule,
        string retryAfter
    )
    {
        var message = string.IsNullOrWhiteSpace(_options.QuotaExceededMessage)
            ? $"Slow down! Too many requests. Try again in {rule.Period}."
            : _options.QuotaExceededMessage;
        httpContext.Response.Headers["Retry-After"] = retryAfter;
        httpContext.Response.StatusCode = _options.HttpStatusCode;
        var errorModel = new ErrorResponseModel { Message = message };
        return httpContext.Response.WriteAsJsonAsync(errorModel, httpContext.RequestAborted);
    }
}

using AspNetCoreRateLimit;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Bit.Core.Utilities
{
    public class CustomIpRateLimitMiddleware : IpRateLimitMiddleware
    {
        private readonly IpRateLimitOptions _options;
        private readonly IMemoryCache _memoryCache;
        private readonly IBlockIpService _blockIpService;
        private readonly ILogger<IpRateLimitMiddleware> _logger;

        public CustomIpRateLimitMiddleware(
            IMemoryCache memoryCache,
            IBlockIpService blockIpService,
            RequestDelegate next,
            IOptions<IpRateLimitOptions> options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore,
            ILogger<IpRateLimitMiddleware> logger,
            IIpAddressParser ipParser = null)
            : base(next, options, counterStore, policyStore, logger, ipParser)
        {
            _memoryCache = memoryCache;
            _blockIpService = blockIpService;
            _options = options.Value;
            _logger = logger;
        }

        public override Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter)
        {
            var message = string.IsNullOrWhiteSpace(_options.QuotaExceededMessage) ?
                $"Slow down! Too many requests. Try again in {rule.Period}." : _options.QuotaExceededMessage;
            httpContext.Response.Headers["Retry-After"] = retryAfter;
            httpContext.Response.StatusCode = _options.HttpStatusCode;

            httpContext.Response.ContentType = "application/json";
            var errorModel = new ErrorResponseModel { Message = message };
            return httpContext.Response.WriteAsync(JsonConvert.SerializeObject(errorModel));
        }

        public override void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity,
            RateLimitCounter counter, RateLimitRule rule)
        {
            base.LogBlockedRequest(httpContext, identity, counter, rule);
            var key = $"blockedIp_{identity.ClientIp}";

            _memoryCache.TryGetValue(key, out int blockedCount);

            blockedCount++;
            if(blockedCount > 10)
            {
                _blockIpService.BlockIpAsync(identity.ClientIp, false);
                _logger.LogInformation($"Blocked {identity.ClientIp} with token {GetToken(httpContext.Request)}");
            }
            else
            {
                _memoryCache.Set(key, blockedCount,
                    new MemoryCacheEntryOptions().SetSlidingExpiration(new TimeSpan(0, 5, 0)));
            }
        }

        private string GetToken(HttpRequest request)
        {
            if(request == null)
            {
                return null;
            }

            var authorization = request.Headers["Authorization"].FirstOrDefault();
            if(string.IsNullOrWhiteSpace(authorization))
            {
                // Bearer token could exist in the 'Content-Language' header on clients that want to avoid pre-flights.
                var languageAuth = request.Headers["Content-Language"].FirstOrDefault();
                if(string.IsNullOrWhiteSpace(languageAuth) ||
                    !languageAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return request.Query["access_token"].FirstOrDefault();
                }
                else
                {
                    authorization = languageAuth.Split(',')[0];
                }
            }

            return authorization;
        }
    }
}

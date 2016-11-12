using AspNetCoreRateLimit;
using Bit.Api.Models.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Bit.Api.Middleware
{
    public class CustomIpRateLimitMiddleware : IpRateLimitMiddleware
    {
        private readonly IpRateLimitOptions _options;

        public CustomIpRateLimitMiddleware(
            RequestDelegate next,
            IOptions<IpRateLimitOptions> options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore,
            ILogger<IpRateLimitMiddleware> logger,
            IIpAddressParser ipParser = null
            ) : base(next, options, counterStore, policyStore, logger, ipParser)
        {
            _options = options.Value;
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
    }
}

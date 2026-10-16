using System.Globalization;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Limits each partnership to <see cref="GlobalSettings.PartnershipSettings.RateLimitRequests"/> requests per fixed
/// <see cref="GlobalSettings.PartnershipSettings.RateLimitWindow"/>. Runs after <see cref="PartnershipAccessFilter"/>
/// has resolved the partnership.
/// </summary>
/// <remarks>
/// Returns 429 with a <c>Retry-After</c> header when the limit is exceeded. Allows the request when the counter store
/// is unavailable.
/// </remarks>
public sealed class PartnershipRateLimitFilter(
    IPartnershipRequestContext partnershipRequestContext,
    IPartnershipRateLimitStore store,
    GlobalSettings globalSettings,
    TimeProvider timeProvider,
    ILogger<PartnershipRateLimitFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var partnershipId = partnershipRequestContext.Partnership.Id;
        var limit = globalSettings.Partnerships.RateLimitRequests;
        var windowMilliseconds = (long)globalSettings.Partnerships.RateLimitWindow.TotalMilliseconds;

        var now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var windowIndex = now / windowMilliseconds;
        var millisecondsUntilReset = (windowIndex + 1) * windowMilliseconds - now;

        long count;
        try
        {
            count = await store.IncrementAsync($"partnership-rate-limit:{partnershipId:N}:{windowIndex}",
                TimeSpan.FromMilliseconds(millisecondsUntilReset));
        }
        catch (Exception exception)
        {
            logger.LogWarning("Partnership rate limit store failed with {ExceptionType}; allowing the request. PartnershipId: {PartnershipId}",
                exception.GetType().FullName, partnershipId);
            await next();
            return;
        }

        if (count > limit)
        {
            var retryAfterSeconds = (long)Math.Ceiling(millisecondsUntilReset / 1000d);
            context.HttpContext.Response.Headers[HeaderNames.RetryAfter] =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Result = PartnershipErrorResults.RateLimited(context.HttpContext);
            return;
        }

        await next();
    }
}

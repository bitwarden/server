using Bit.Core.Services;
using Microsoft.AspNetCore.Http;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// Middleware to extract the x-play-id header and set it in the PlayIdService.
/// 
/// PlayId is used in testing infrastructure to track data created during automated testing and fa  cilitate cleanup.
/// </summary>
/// <param name="next"></param>
public sealed class PlayIdMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context, PlayIdService playIdService)
    {
        if (context.Request.Headers.TryGetValue("x-play-id", out var playId))
        {
            playIdService.PlayId = playId;
        }

        return next(context);
    }
}

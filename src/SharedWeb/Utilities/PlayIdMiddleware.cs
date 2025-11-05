using Bit.Core.Services;
using Microsoft.AspNetCore.Http;

namespace Bit.SharedWeb.Utilities;

public sealed class PlayIdMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context, IPlayIdService playIdService)
    {
        if (context.Request.Headers.TryGetValue("x-play-id", out var playId))
        {
            playIdService.PlayId = playId;
        }

        return next(context);
    }
}

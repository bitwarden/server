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
    private const int MaxPlayIdLength = 256;

    public async Task Invoke(HttpContext context, PlayIdService playIdService)
    {
        if (context.Request.Headers.TryGetValue("x-play-id", out var playId))
        {
            var playIdValue = playId.ToString();

            if (string.IsNullOrWhiteSpace(playIdValue))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = "x-play-id header cannot be empty or whitespace" });
                return;
            }

            if (playIdValue.Length > MaxPlayIdLength)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = $"x-play-id header cannot exceed {MaxPlayIdLength} characters" });
                return;
            }

            playIdService.PlayId = playIdValue;
        }

        await next(context);
    }
}

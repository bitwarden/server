using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.AdminConsole.Partnerships.Idempotency;

/// <summary>
/// Applies <c>Idempotency-Key</c> handling to an action marked with <see cref="PartnershipIdempotentAttribute"/>.
/// </summary>
public sealed class PartnershipIdempotencyFilter(
    PartnershipIdempotencyStore store,
    IPartnershipRequestContext partnershipRequestContext) : IAsyncResourceFilter, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int MaxKeyLength = 255;

    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        // Model binding reads the body before action filters run, so keep it rewindable for hashing.
        if (context.HttpContext.Request.Headers.ContainsKey(HeaderName))
        {
            context.HttpContext.Request.EnableBuffering();
        }

        return next();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next();
            return;
        }

        if (values.Count != 1 || !IsValidKey(values[0]))
        {
            context.Result = PartnershipErrorResults.InvalidIdempotencyKey(httpContext);
            return;
        }

        var key = values[0]!;
        var partnershipId = partnershipRequestContext.Partnership.Id;
        var requestHash = await ComputeRequestHashAsync(httpContext.Request);
        var claim = await store.ClaimAsync(partnershipId, key, requestHash);

        switch (claim.Outcome)
        {
            case PartnershipIdempotencyOutcome.Replay:
                httpContext.Response.Headers[ReplayedHeaderName] = "true";
                context.Result = new StoredResponseResult(claim.Response!);
                return;
            case PartnershipIdempotencyOutcome.KeyReused:
                context.Result = PartnershipErrorResults.IdempotencyKeyReused(httpContext);
                return;
            case PartnershipIdempotencyOutcome.InProgress:
                context.Result = PartnershipErrorResults.IdempotencyKeyInProgress(httpContext);
                return;
        }

        PartnershipStoredResponse? response = null;
        try
        {
            var executed = await next();
            if ((executed.Exception is null || executed.ExceptionHandled) && executed.Result is { } result)
            {
                response = await CaptureAsync(executed, result);
                executed.Result = new StoredResponseResult(response);
            }
        }
        finally
        {
            if (response is not null && IsStorable(response.StatusCode))
            {
                await store.CompleteAsync(partnershipId, key, requestHash, response);
            }
            else
            {
                await store.ReleaseAsync(partnershipId, key);
            }
        }
    }

    private static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= MaxKeyLength } && key.All(c => c is >= ' ' and <= '~');

    /// <summary>
    /// 2xx and deterministic 4xx outcomes are replayed. 5xx and 429 are not, so the partner can retry with the same key.
    /// </summary>
    private static bool IsStorable(int statusCode) =>
        statusCode is >= 200 and < 300 or (>= 400 and < 500 and not StatusCodes.Status429TooManyRequests);

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n"));

        request.Body.Position = 0;
        var buffer = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(buffer)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }
        request.Body.Position = 0;

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task<PartnershipStoredResponse> CaptureAsync(ActionContext context, IActionResult result)
    {
        var response = context.HttpContext.Response;
        var originalBody = response.Body;
        using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            await result.ExecuteResultAsync(context);
            await response.BodyWriter.FlushAsync();
        }
        finally
        {
            response.Body = originalBody;
        }

        return new PartnershipStoredResponse(response.StatusCode, response.ContentType, buffer.ToArray());
    }

    private sealed class StoredResponseResult(PartnershipStoredResponse stored) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = stored.StatusCode;
            response.ContentType = stored.ContentType;
            response.ContentLength = stored.Body.Length;
            await response.Body.WriteAsync(stored.Body);
        }
    }
}

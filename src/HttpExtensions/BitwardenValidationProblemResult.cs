using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// A Bitwarden-flavored RFC 7807 validation problem result. Wraps an inner
/// <see cref="ProblemHttpResult"/> so we have room to grow — for example, implementing
/// <c>IEndpointMetadataProvider</c> for OpenAPI — without changing the public signature of
/// <c>TypedResults.BitwardenValidationProblem</c>.
/// </summary>
public sealed class BitwardenValidationProblemResult :
    IResult,
    IStatusCodeHttpResult,
    IContentTypeHttpResult,
    IValueHttpResult,
    IValueHttpResult<ProblemDetails>
{
    private readonly ProblemHttpResult _inner;

    internal BitwardenValidationProblemResult(ProblemHttpResult inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public ProblemDetails ProblemDetails => _inner.ProblemDetails;

    public int? StatusCode => _inner.StatusCode;

    public string? ContentType => _inner.ContentType;

    object? IValueHttpResult.Value => _inner.ProblemDetails;

    ProblemDetails? IValueHttpResult<ProblemDetails>.Value => _inner.ProblemDetails;

    public Task ExecuteAsync(HttpContext httpContext) => _inner.ExecuteAsync(httpContext);
}

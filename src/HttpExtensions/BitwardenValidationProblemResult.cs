using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Bit.HttpExtensions;

/// <summary>
/// A Bitwarden-flavored RFC 7807 validation problem result. Wraps an inner
/// <see cref="ProblemHttpResult"/> so we have room to grow without changing the public signature of
/// <c>TypedResults.BitwardenValidationProblem</c>.
/// </summary>
public sealed class BitwardenValidationProblemResult :
    IResult,
    IEndpointMetadataProvider,
    IStatusCodeHttpResult,
    IContentTypeHttpResult,
    IValueHttpResult,
    IValueHttpResult<ProblemDetails>
{
    private readonly ProblemHttpResult _inner;

    internal BitwardenValidationProblemResult(BitwardenValidationProblemDetails problemDetails)
    {
        ArgumentNullException.ThrowIfNull(problemDetails);
        _inner = TypedResults.Problem(problemDetails);
        ProblemDetails = problemDetails;
    }

    public BitwardenValidationProblemDetails ProblemDetails { get; }

    public int? StatusCode => _inner.StatusCode;

    public string? ContentType => _inner.ContentType;

    object? IValueHttpResult.Value => ProblemDetails;

    ProblemDetails? IValueHttpResult<ProblemDetails>.Value => ProblemDetails;

    public Task ExecuteAsync(HttpContext httpContext) => _inner.ExecuteAsync(httpContext);

    /// <summary>
    /// Declares the problem document on every endpoint whose handler can return this result, so a client reads
    /// the coded shape out of OpenAPI rather than out of our source.
    /// </summary>
    /// <remarks>
    /// Declares 400, the status the result defaults to. An endpoint that carries this same body on another
    /// status — 409 for a state conflict — declares that one itself with <c>Produces</c>.
    /// </remarks>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status400BadRequest,
            typeof(BitwardenValidationProblemDetails),
            ["application/problem+json"]));
    }
}

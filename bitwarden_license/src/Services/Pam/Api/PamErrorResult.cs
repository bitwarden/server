using System.Reflection;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using ErrorCode = Microsoft.AspNetCore.Http.HttpResults.BitwardenTypedResultsExtensions.ErrorCode;

namespace Bit.Services.Pam.Api;

/// <summary>
/// The one shape every failure a PAM handler <em>chooses</em> to return comes out as: an RFC 7807 problem response
/// carrying the error's stable <see cref="IValidationError.Type"/> code, keyed by the request property it names.
/// </summary>
/// <remarks>
/// <para>
/// This is the single error arm of every PAM handler's <c>Results&lt;…&gt;</c> return type, so a handler declares
/// "this succeeds or it fails as a PAM error" and the mapping from a domain <see cref="Error"/> to a status code
/// lives here rather than being restated per endpoint.
/// </para>
/// <para>
/// Status codes: a coded failure is a 400 unless it is a <see cref="ConflictError"/>, which is a 409 — the request
/// was well formed and the state was not what it needed. Both carry the same body, because the code, not the
/// status, is what a client switches on; the status is a coarse hint for anything that reads no further.
/// </para>
/// <para>
/// A <see cref="NotFoundError"/> is deliberately <em>not</em> a problem response. It stays the
/// <see cref="ErrorResponseModel"/> 404 that <c>WithBasicExceptionHandling</c> produces for a thrown
/// <c>NotFoundException</c>, so PAM's 404s read the same however they were reached. Nothing is lost: a 404 needs no
/// code, since the status already tells it apart from every other failure. Codes exist for the failures a status
/// cannot separate.
/// </para>
/// </remarks>
public sealed class PamErrorResult : IResult, IEndpointMetadataProvider
{
    private const string ValidationProblemType = "validation_error";
    private const string ConflictProblemType = "conflict_error";

    private readonly Error _error;

    private PamErrorResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _error = error;
    }

    /// <summary>Wraps a domain error as the failure arm of a handler's result.</summary>
    public static PamErrorResult From(Error error) => new(error);

    /// <summary>The error this result reports. Exposed so tests can assert on it without executing the response.</summary>
    public Error Error => _error;

    public Task ExecuteAsync(HttpContext httpContext) => ToResult(_error).ExecuteAsync(httpContext);

    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Declared here rather than left to the group's WithBasicExceptionHandling metadata, which describes the
        // ErrorResponseModel these two statuses no longer carry. The `errors` extension member is not in the
        // ProblemDetails schema — the same gap BitwardenValidationProblemResult leaves — so the spec says
        // "a problem response" without spelling out the codes.
        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status400BadRequest, typeof(ProblemDetails), ["application/problem+json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status409Conflict, typeof(ProblemDetails), ["application/problem+json"]));
        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status404NotFound, typeof(ErrorResponseModel), ["application/json"]));
    }

    private static IResult ToResult(Error error) => error switch
    {
        IValidationError validationError => Problem(validationError, error is ConflictError),
        NotFoundError notFound => TypedResults.NotFound(new ErrorResponseModel(notFound.Message)),
        // An uncoded error reaching here is a gap in the catalog rather than a state a client can act on, so it
        // degrades to the envelope the exception filter would have produced instead of inventing a code.
        BadRequestError badRequest => TypedResults.BadRequest(new ErrorResponseModel(badRequest.Message)),
        ConflictError conflict => TypedResults.Json(
            new ErrorResponseModel(conflict.Message), statusCode: StatusCodes.Status409Conflict),
        _ => TypedResults.Json(
            new ErrorResponseModel(error.Message), statusCode: StatusCodes.Status500InternalServerError),
    };

    private static IResult Problem(IValidationError error, bool isConflict) =>
        TypedResults.BitwardenValidationProblem(
            errors: new Dictionary<string, ErrorCode[]>
            {
                { error.PropertyName, [new ErrorCode(error.Type, error.Message)] },
            },
            title: isConflict
                ? "The request conflicts with the current state."
                : "One or more validation errors occurred.",
            type: isConflict ? ConflictProblemType : ValidationProblemType,
            statusCode: isConflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
}

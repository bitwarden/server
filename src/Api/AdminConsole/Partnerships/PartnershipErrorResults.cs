using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Utilities.v2;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Builds <see cref="PartnershipErrorResponseModel"/> responses. This is the only place partnership errors are mapped
/// to HTTP status codes.
/// </summary>
public static class PartnershipErrorResults
{
    public const string InvalidRequestCode = "invalid_request";
    public const string UnauthorizedCode = "unauthorized";
    public const string ForbiddenCode = "forbidden";
    public const string NotFoundCode = "not_found";
    public const string ConflictCode = "conflict";
    public const string RateLimitedCode = "rate_limited";
    public const string InternalErrorCode = "internal_error";

    public const string InvalidRequestMessage = "The request body is missing or invalid.";
    public const string UnauthorizedMessage = "Authenticate with the partner organization's API key.";
    public const string ForbiddenMessage = "This credential can't use the partnership API.";
    public const string RateLimitedMessage = "Too many requests. Retry later.";
    public const string InternalErrorMessage = "An unexpected error occurred. Contact support with the request ID.";

    /// <summary>
    /// Maps a domain <see cref="Error"/> to its status code and envelope. Uses the error's
    /// <see cref="IPartnershipError.Code"/> when it has one.
    /// </summary>
    public static ObjectResult FromError(Error error, HttpContext httpContext) =>
        error switch
        {
            EffectiveAtInFuture e => Create(StatusCodes.Status422UnprocessableEntity, e.Code, e.Message, httpContext),
            BadRequestError e => Create(StatusCodes.Status400BadRequest, CodeOf(e, InvalidRequestCode), e.Message,
                httpContext),
            NotFoundError e => Create(StatusCodes.Status404NotFound, CodeOf(e, NotFoundCode), e.Message, httpContext),
            ConflictError e => Create(StatusCodes.Status409Conflict, CodeOf(e, ConflictCode), e.Message, httpContext),
            _ => InternalError(httpContext),
        };

    /// <summary>
    /// 400 for a request that failed model binding or validation before reaching the domain.
    /// </summary>
    public static ObjectResult InvalidRequest(HttpContext httpContext) =>
        Create(StatusCodes.Status400BadRequest, InvalidRequestCode, InvalidRequestMessage, httpContext);

    /// <summary>
    /// 401 for a request with no valid access token.
    /// </summary>
    public static ObjectResult Unauthorized(HttpContext httpContext) =>
        Create(StatusCodes.Status401Unauthorized, UnauthorizedCode, UnauthorizedMessage, httpContext);

    /// <summary>
    /// 403 for an access token that isn't an organization API key.
    /// </summary>
    public static ObjectResult Forbidden(HttpContext httpContext) =>
        Create(StatusCodes.Status403Forbidden, ForbiddenCode, ForbiddenMessage, httpContext);

    /// <summary>
    /// 403 for an authenticated partner whose partnership is inactive. Commands report the same error as 409 through
    /// <see cref="FromError"/>.
    /// </summary>
    public static ObjectResult PartnershipNotActive(HttpContext httpContext)
    {
        var error = new PartnershipNotActive();
        return Create(StatusCodes.Status403Forbidden, error.Code, error.Message, httpContext);
    }

    /// <summary>
    /// 429 for a request rejected by partnership rate limiting.
    /// </summary>
    public static ObjectResult RateLimited(HttpContext httpContext) =>
        Create(StatusCodes.Status429TooManyRequests, RateLimitedCode, RateLimitedMessage, httpContext);

    /// <summary>
    /// 500 with a generic message. Never carries exception or error detail.
    /// </summary>
    public static ObjectResult InternalError(HttpContext httpContext) =>
        Create(StatusCodes.Status500InternalServerError, InternalErrorCode, InternalErrorMessage, httpContext);

    private static string CodeOf(Error error, string fallback) =>
        error is IPartnershipError partnershipError ? partnershipError.Code : fallback;

    private static ObjectResult Create(int statusCode, string code, string message, HttpContext httpContext) =>
        new(new PartnershipErrorResponseModel(code, message, PartnershipRequestId.Get(httpContext)))
        {
            StatusCode = statusCode,
        };
}

using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bit.ExceptionHandling;

/// <summary>
/// Extension methods for adding Bitwarden exception handling to Minimal API endpoint groups.
/// </summary>
public static class ExceptionHandlingEndpointExtensions
{
    private static readonly ProducesResponseTypeMetadata[] _errorResponses =
    [
        new(StatusCodes.Status400BadRequest, typeof(ErrorResponseModel), ["application/json"]),
        new(StatusCodes.Status401Unauthorized, typeof(ErrorResponseModel), ["application/json"]),
        new(StatusCodes.Status402PaymentRequired, typeof(ErrorResponseModel), ["application/json"]),
        new(StatusCodes.Status404NotFound, typeof(ErrorResponseModel), ["application/json"]),
        new(StatusCodes.Status409Conflict, typeof(ErrorResponseModel), ["application/json"]),
        new(StatusCodes.Status500InternalServerError, typeof(ErrorResponseModel), ["application/json"]),
    ];

    /// <summary>
    /// Adds the Bitwarden exception handling filter to the builder, mirroring the behavior of
    /// <c>ExceptionHandlerFilterAttribute</c> used by MVC controllers. Place this before other filters on the
    /// group so it wraps the full endpoint pipeline.
    /// </summary>
    /// <remarks>
    /// Differences from <c>ExceptionHandlerFilterAttribute</c>:
    /// <list type="bullet">
    ///   <item><description>
    ///     Stripe-specific exceptions and <c>SecurityTokenValidationException</c> are not handled here.
    ///   </description></item>
    ///   <item><description>
    ///     Always produces the internal <see cref="Bit.Core.Models.Api.ErrorResponseModel"/> shape; there is
    ///     no public-API mode.
    ///   </description></item>
    ///   <item><description>
    ///     Exception message, stack trace, and inner-exception message in <c>Development</c> are attached only
    ///     to the unhandled 500 response, never to a modelled 400/401/402/404/409.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="AggregateException"/> is not handled and falls through to the default 500 branch.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public static TBuilder WithBasicExceptionHandling<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter<TBuilder, ExceptionHandlerEndpointFilter>();
        builder.WithMetadata(_errorResponses);
        return builder;
    }
}

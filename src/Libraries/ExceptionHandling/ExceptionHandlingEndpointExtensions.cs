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
    /// Adds the Bitwarden exception handling filter to the builder. The filter translates thrown exceptions into
    /// <see cref="Bit.Core.Models.Api.ErrorResponseModel"/> responses with appropriate HTTP status codes,
    /// mirroring the behavior of <c>ExceptionHandlerFilterAttribute</c> used by MVC controllers.
    /// Place this before other filters on the group so it wraps the full endpoint pipeline.
    /// </summary>
    /// <remarks>
    /// Differences from <c>ExceptionHandlerFilterAttribute</c>:
    /// <list type="bullet">
    ///   <item><description>
    ///     Stripe-specific exceptions (<c>StripeException</c>, <c>GatewayException</c>,
    ///     <c>BillingException</c>) are not handled and fall through to the default 500 branch.
    ///   </description></item>
    ///   <item><description>
    ///     <c>SecurityTokenValidationException</c> (403 Forbidden) is not handled; token validation
    ///     failures are expected to be caught by authentication middleware before reaching the endpoint.
    ///   </description></item>
    ///   <item><description>
    ///     Always produces the internal <see cref="Bit.Core.Models.Api.ErrorResponseModel"/> shape;
    ///     there is no public-API mode.
    ///   </description></item>
    ///   <item><description>
    ///     In <c>Development</c> the exception message, stack trace and inner-exception message are attached only
    ///     to the unhandled 500 response, never to a modelled 400/401/402/404/409. The MVC filter attaches them to
    ///     every response it shapes, which put the server's call stack and absolute source paths into the client's
    ///     console on an ordinary rejection (PM-42634).
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="AggregateException"/> is not handled and falls through to the default 500 branch.
    ///     The known throw sites in the codebase (organization invite, license validation) accumulate
    ///     inner exceptions via bare <c>catch (Exception)</c> blocks, so the inner exceptions are
    ///     uncontrolled infrastructure errors rather than typed user-facing messages. Mapping them to
    ///     400 and surfacing their messages would leak internal detail.
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

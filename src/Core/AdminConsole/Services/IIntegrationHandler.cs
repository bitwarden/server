using System.Globalization;
using System.Net;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;

namespace Bit.Core.Services;

public interface IIntegrationHandler
{
    Task<IntegrationHandlerResult> HandleAsync(string json);
}

public interface IIntegrationHandler<T> : IIntegrationHandler
{
    Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<T> message);
}

public abstract class IntegrationHandlerBase<T> : IIntegrationHandler<T>
{
    public async Task<IntegrationHandlerResult> HandleAsync(string json)
    {
        var message = IntegrationMessage<T>.FromJson(json);
        return await HandleAsync(message ?? throw new ArgumentException("IntegrationMessage was null when created from the provided JSON"));
    }

    public abstract Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<T> message);

    protected IntegrationHandlerResult ResultFromHttpResponse(
        HttpResponseMessage response,
        IntegrationMessage<T> message,
        TimeProvider timeProvider)
    {
        if (response.IsSuccessStatusCode)
        {
            return IntegrationHandlerResult.Succeed(message);
        }

        var category = ClassifyHttpStatusCode(response.StatusCode);
        var failureReason = response.ReasonPhrase ?? $"Failure with status code {(int)response.StatusCode}";

        if (category is not (IntegrationFailureCategory.RateLimited
                or IntegrationFailureCategory.TransientError
                or IntegrationFailureCategory.ServiceUnavailable) ||
            !response.Headers.TryGetValues("Retry-After", out var values)
           )
        {
            return IntegrationHandlerResult.Fail(message: message, category: category, failureReason: failureReason);
        }

        // Handle Retry-After header for rate-limited and retryable errors
        DateTime? delayUntil = null;
        var value = values.FirstOrDefault();
        if (int.TryParse(value, out var seconds))
        {
            // Retry-after was specified in seconds
            delayUntil = timeProvider.GetUtcNow().AddSeconds(seconds).UtcDateTime;
        }
        else if (DateTimeOffset.TryParseExact(value,
                     "r", // "r" is the round-trip format: RFC1123
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                     out var retryDate))
        {
            // Retry-after was specified as a date
            delayUntil = retryDate.UtcDateTime;
        }

        return IntegrationHandlerResult.Fail(
            message,
            category,
            failureReason,
            delayUntil
        );
    }

    /// <summary>
    /// Classifies an <see cref="HttpStatusCode"/> as an <see cref="IntegrationFailureCategory"/> to drive
    /// retry behavior and operator-facing failure reporting.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The corresponding <see cref="IntegrationFailureCategory"/>.</returns>
    protected static IntegrationFailureCategory ClassifyHttpStatusCode(HttpStatusCode statusCode)
    {
        var explicitCategory = statusCode switch
        {
            HttpStatusCode.Unauthorized => IntegrationFailureCategory.AuthenticationFailed,
            HttpStatusCode.Forbidden => IntegrationFailureCategory.AuthenticationFailed,
            HttpStatusCode.NotFound => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.Gone => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.MovedPermanently => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.TemporaryRedirect => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.PermanentRedirect => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.TooManyRequests => IntegrationFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => IntegrationFailureCategory.TransientError,
            HttpStatusCode.InternalServerError => IntegrationFailureCategory.TransientError,
            HttpStatusCode.BadGateway => IntegrationFailureCategory.TransientError,
            HttpStatusCode.GatewayTimeout => IntegrationFailureCategory.TransientError,
            HttpStatusCode.ServiceUnavailable => IntegrationFailureCategory.ServiceUnavailable,
            HttpStatusCode.NotImplemented => IntegrationFailureCategory.PermanentFailure,
            _ => (IntegrationFailureCategory?)null
        };

        if (explicitCategory is not null)
        {
            return explicitCategory.Value;
        }

        return (int)statusCode switch
        {
            >= 300 and <= 399 => IntegrationFailureCategory.ConfigurationError,
            >= 400 and <= 499 => IntegrationFailureCategory.ConfigurationError,
            >= 500 and <= 599 => IntegrationFailureCategory.ServiceUnavailable,
            _ => IntegrationFailureCategory.ServiceUnavailable
        };
    }
}

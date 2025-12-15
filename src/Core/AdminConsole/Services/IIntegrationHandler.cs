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
        DateTime? delayUntil = null;

        // Handle Retry-After header for rate-limited and retryable errors
        if (category is IntegrationFailureCategory.RateLimited or IntegrationFailureCategory.TransientError)
        {
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
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
            }
        }

        return IntegrationHandlerResult.Fail(
            message,
            category,
            failureReason,
            delayUntil
        );
    }

    protected static IntegrationFailureCategory ClassifyHttpStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => IntegrationFailureCategory.AuthenticationFailed,
            HttpStatusCode.Forbidden => IntegrationFailureCategory.AuthenticationFailed,
            HttpStatusCode.NotFound => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.TemporaryRedirect => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.PermanentRedirect => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.MovedPermanently => IntegrationFailureCategory.ConfigurationError,
            HttpStatusCode.TooManyRequests => IntegrationFailureCategory.RateLimited,
            HttpStatusCode.ServiceUnavailable => IntegrationFailureCategory.ServiceUnavailable,
            HttpStatusCode.RequestTimeout => IntegrationFailureCategory.TransientError,
            HttpStatusCode.InternalServerError => IntegrationFailureCategory.TransientError,
            HttpStatusCode.BadGateway => IntegrationFailureCategory.TransientError,
            HttpStatusCode.GatewayTimeout => IntegrationFailureCategory.TransientError,
            _ => IntegrationFailureCategory.ServiceUnavailable
        };
    }
}



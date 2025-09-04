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
        return await HandleAsync(message ?? throw new ArgumentException("IntegrationMessage was null when created from the provided json"));
    }

    public abstract Task<IntegrationHandlerResult> HandleAsync(IntegrationMessage<T> message);

    protected IntegrationHandlerResult ResultFromHttpResponse(
        HttpResponseMessage response,
        IntegrationMessage<T> message,
        TimeProvider timeProvider)
    {
        var result = new IntegrationHandlerResult(success: response.IsSuccessStatusCode, message);

        if (response.IsSuccessStatusCode) return result;

        switch (response.StatusCode)
        {
            case HttpStatusCode.TooManyRequests:
            case HttpStatusCode.RequestTimeout:
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                result.Retryable = true;
                result.FailureReason = response.ReasonPhrase ?? $"Failure with status code: {(int)response.StatusCode}";

                if (response.Headers.TryGetValues("Retry-After", out var values))
                {
                    var value = values.FirstOrDefault();
                    if (int.TryParse(value, out var seconds))
                    {
                        // Retry-after was specified in seconds. Adjust DelayUntilDate by the requested number of seconds.
                        result.DelayUntilDate = timeProvider.GetUtcNow().AddSeconds(seconds).UtcDateTime;
                    }
                    else if (DateTimeOffset.TryParseExact(value,
                                 "r", // "r" is the round-trip format: RFC1123
                                 CultureInfo.InvariantCulture,
                                 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                 out var retryDate))
                    {
                        // Retry-after was specified as a date. Adjust DelayUntilDate to the specified date.
                        result.DelayUntilDate = retryDate.UtcDateTime;
                    }
                }
                break;
            default:
                result.Retryable = false;
                result.FailureReason = response.ReasonPhrase ?? $"Failure with status code {(int)response.StatusCode}";
                break;
        }

        return result;
    }
}

using System.Text.Json;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Integrations;

public interface IIntegrationMessage
{
    IntegrationType IntegrationType { get; }
    int RetryCount { get; set; }
    DateTime? NotBeforeUtc { get; set; }
    void ApplyRetry(DateTime? handlerNotBeforeUtc);
    string ToJson();
}

public class IntegrationMessage<T> : IIntegrationMessage
{
    public IntegrationType IntegrationType { get; set; }
    public T Configuration { get; set; }
    public string RenderedTemplate { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NotBeforeUtc { get; set; }

    public void ApplyRetry(DateTime? handlerNotBeforeUtc)
    {
        RetryCount++;

        var baseTime = handlerNotBeforeUtc ?? DateTime.UtcNow;
        var backoffSeconds = Math.Pow(2, RetryCount);
        var jitterSeconds = Random.Shared.Next(0, 3);

        NotBeforeUtc = baseTime.AddSeconds(backoffSeconds + jitterSeconds);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static IntegrationMessage<T> FromJson(string json)
    {
        return JsonSerializer.Deserialize<IntegrationMessage<T>>(json)
               ?? throw new InvalidOperationException("Invalid JSON payload.");
    }
}

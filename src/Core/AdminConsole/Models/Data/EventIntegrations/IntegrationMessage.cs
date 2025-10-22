using System.Text.Json;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationMessage : IIntegrationMessage
{
    public IntegrationType IntegrationType { get; set; }
    public required string MessageId { get; set; }
    public required string OrganizationId { get; set; }
    public required string RenderedTemplate { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? DelayUntilDate { get; set; }

    public void ApplyRetry(DateTime? handlerDelayUntilDate)
    {
        RetryCount++;

        var baseTime = handlerDelayUntilDate ?? DateTime.UtcNow;
        var backoffSeconds = Math.Pow(2, RetryCount);
        var jitterSeconds = Random.Shared.Next(0, 3);

        DelayUntilDate = baseTime.AddSeconds(backoffSeconds + jitterSeconds);
    }

    public virtual string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
}

public class IntegrationMessage<T> : IntegrationMessage
{
    public required T Configuration { get; set; }

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static IntegrationMessage<T>? FromJson(string json)
    {
        return JsonSerializer.Deserialize<IntegrationMessage<T>>(json);
    }
}

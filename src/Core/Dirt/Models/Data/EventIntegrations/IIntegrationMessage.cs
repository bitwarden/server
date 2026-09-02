using Bit.Core.Dirt.Enums;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public interface IIntegrationMessage
{
    IntegrationType IntegrationType { get; }
    string MessageId { get; set; }
    string? OrganizationId { get; set; }
    int RetryCount { get; }
    DateTime? DelayUntilDate { get; }
    void ApplyRetry(DateTime? handlerDelayUntilDate);
    string ToJson();
}

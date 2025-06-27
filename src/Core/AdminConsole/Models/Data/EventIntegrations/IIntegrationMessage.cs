#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public interface IIntegrationMessage
{
    IntegrationType IntegrationType { get; }
    string MessageId { get; set; }
    int RetryCount { get; }
    DateTime? DelayUntilDate { get; }
    void ApplyRetry(DateTime? handlerDelayUntilDate);
    string ToJson();
}

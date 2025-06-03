using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.Integrations;

public interface IIntegrationMessage
{
    IntegrationType IntegrationType { get; }
    int RetryCount { get; set; }
    DateTime? DelayUntilDate { get; set; }
    void ApplyRetry(DateTime? handlerDelayUntilDate);
    string ToJson();
}

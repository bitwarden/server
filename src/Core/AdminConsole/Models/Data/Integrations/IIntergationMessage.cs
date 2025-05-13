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

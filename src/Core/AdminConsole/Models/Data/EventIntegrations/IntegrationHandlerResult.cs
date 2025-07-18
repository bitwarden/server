#nullable enable

namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

public class IntegrationHandlerResult
{
    public IntegrationHandlerResult(bool success, IIntegrationMessage message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; set; } = false;
    public bool Retryable { get; set; } = false;
    public IIntegrationMessage Message { get; set; }
    public DateTime? DelayUntilDate { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}

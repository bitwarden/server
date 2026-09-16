using Bit.Core.Dirt.Models.Data.EventIntegrations;

namespace Bit.Core.Dirt.Services;

public record HecVerificationResult(bool Success, string? FailureReason);

public interface IHecIntegrationVerificationService
{
    Task<HecVerificationResult> VerifyAsync(HecIntegration integration, Guid organizationId);
}

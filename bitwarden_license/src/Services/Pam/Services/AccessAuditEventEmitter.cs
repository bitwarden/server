using Bit.Core;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly ILogger<AccessAuditEventEmitter> _logger;

    public AccessAuditEventEmitter(
        Bitwarden.Server.Sdk.Features.IFeatureService featureService,
        IAccessAuditEventRepository accessAuditEventRepository,
        ILogger<AccessAuditEventEmitter> logger)
    {
        _featureService = featureService;
        _accessAuditEventRepository = accessAuditEventRepository;
        _logger = logger;
    }

    public async Task EmitAsync(AccessAuditEventData auditEvent)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            try
            {
                await _accessAuditEventRepository.CreateAsync(auditEvent);
            }
            catch (Exception ex) when (auditEvent.Phase == AccessAuditEventPhase.Outcome)
            {
                _logger.LogError(ex,
                    "Failed to record the outcome of PAM audit event {Kind} ({CorrelationId}). The trail shows the action as incomplete.",
                    auditEvent.Kind, auditEvent.CorrelationId);
            }
        }
    }
}

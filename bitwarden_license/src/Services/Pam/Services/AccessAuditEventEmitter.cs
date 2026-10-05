using Bit.Core;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;

    public AccessAuditEventEmitter(
        Bitwarden.Server.Sdk.Features.IFeatureService featureService,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        _featureService = featureService;
        _accessAuditEventRepository = accessAuditEventRepository;
    }

    public async Task EmitAsync(AccessAuditEventData auditEvent)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            await _accessAuditEventRepository.CreateAsync(auditEvent);
        }
    }
}

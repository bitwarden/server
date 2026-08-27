using Bit.Core;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    private readonly IFeatureService _featureService;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;

    public AccessAuditEventEmitter(
        IFeatureService featureService,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        _featureService = featureService;
        _accessAuditEventRepository = accessAuditEventRepository;
    }

    public Task EmitAsync(AccessAuditEventData auditEvent)
    {
        // The kill switch is read per call rather than at registration so flipping it takes effect on the next
        // request instead of the next deployment — the whole point of having it. Dropping the event here, at the
        // seam, is what keeps the commands unaware: they still await the emit and their outcome is unchanged.
        if (_featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            return Task.CompletedTask;
        }

        // Persist to the dedicated PAM audit store. Deliberately not enlisted in the caller's transaction (there is
        // none): under the before/after model the Attempt is written ahead of the action and the Outcome after it, so a
        // failure in between leaves an in-doubt Attempt rather than a silently lost event. Fanning out to the
        // organization event log is deferred.
        return _accessAuditEventRepository.CreateAsync(auditEvent);
    }
}

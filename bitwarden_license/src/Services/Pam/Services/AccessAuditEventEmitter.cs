using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;

    public AccessAuditEventEmitter(IAccessAuditEventRepository accessAuditEventRepository)
    {
        _accessAuditEventRepository = accessAuditEventRepository;
    }

    public Task EmitAsync(AccessAuditEventData auditEvent) =>
        // Persist to the dedicated PAM audit store. Deliberately not enlisted in the caller's transaction (there is
        // none): under the before/after model the Attempt is written ahead of the action and the Outcome after it, so a
        // failure in between leaves an in-doubt Attempt rather than a silently lost event. Fanning out to the
        // organization event log is deferred.
        _accessAuditEventRepository.CreateAsync(auditEvent);
}

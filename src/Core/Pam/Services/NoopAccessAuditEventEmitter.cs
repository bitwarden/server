using Bit.Pam.Models;

namespace Bit.Pam.Services;

/// <summary>
/// Default fallback for <see cref="IAccessAuditEventEmitter"/>. PAM audit persistence is a commercial feature, so in
/// builds without the commercial implementation the emitter records nothing, matching behaviour when the <c>Pam</c>
/// feature flag is off. The real fan-out (PAM audit store + organization event log) lives in the commercial Pam
/// library.
/// </summary>
public class NoopAccessAuditEventEmitter : IAccessAuditEventEmitter
{
    public Task EmitAsync(AccessAuditEventData auditEvent) => Task.CompletedTask;
}

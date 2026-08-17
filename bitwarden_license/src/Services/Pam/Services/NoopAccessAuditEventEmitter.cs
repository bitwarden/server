using Bit.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>
/// Placeholder implementation of <see cref="IAccessAuditEventEmitter"/> that records nothing.
///
/// The PAM commands are wired to emit audit events, but the audit store they would be written to is not part of this
/// slice. This keeps the emitting call sites — and the DI graph the commands resolve through — intact until the store
/// lands, at which point only the registration changes.
/// </summary>
public class NoopAccessAuditEventEmitter : IAccessAuditEventEmitter
{
    public Task EmitAsync(AccessAuditEventData auditEvent) => Task.CompletedTask;
}

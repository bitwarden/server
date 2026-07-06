namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamDaemon"/>. Only an <see cref="Enrolled"/> daemon may authenticate, poll, or
/// claim jobs; <see cref="Revoked"/> is terminal for this credential (re-enrollment mints a new one — the deferred
/// spec rule <c>ReissueDaemonCredential</c>).
/// </summary>
public enum PamDaemonStatus : byte
{
    Enrolled = 0,
    Revoked = 1,
}

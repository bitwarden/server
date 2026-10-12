namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamTargetSystem"/>. Only an <see cref="Active"/> target gets rotation jobs
/// offered or claimed.
/// </summary>
public enum PamTargetSystemStatus : byte
{
    Active = 0,
    Disabled = 1,

    /// <summary>
    /// Registered but not yet sealed: <see cref="Entities.PamTargetSystem.SealedData"/> is null. No jobs are offered
    /// or claimed. A <c>PUT</c> that supplies <c>sealedData</c> is the only transition out of this status, moving the
    /// target to <see cref="Active"/>; enable/disable both refuse until then.
    /// </summary>
    PendingSeal = 2,
}

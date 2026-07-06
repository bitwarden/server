namespace Bit.Pam.Enums;

/// <summary>
/// Whether a failed rotation attempt left the target system's password changed, reported alongside a failure so an
/// operator can tell whether the vault credential now disagrees with the target.
/// </summary>
public enum PamRotationSyncState : byte
{
    /// <summary>The target's password is unchanged; the vault credential is still correct.</summary>
    TargetUnchanged = 0,

    /// <summary>The target's password changed but the write to the cipher did not complete; the vault credential is now wrong.</summary>
    TargetUpdated = 1,

    /// <summary>The daemon could not determine whether the target's password changed.</summary>
    Indeterminate = 2,
}

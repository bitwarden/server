namespace Bit.Pam.Enums;

/// <summary>
/// Whether a failed rotation attempt changed the target system's password, so an operator can tell whether the vault
/// credential is now wrong.
/// </summary>
public enum PamRotationSyncState : byte
{
    TargetUnchanged = 0,

    /// <summary>The target's password changed but the cipher write did not complete.</summary>
    TargetUpdated = 1,

    Indeterminate = 2,
}

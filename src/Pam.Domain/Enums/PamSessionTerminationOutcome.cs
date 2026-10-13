namespace Bit.Pam.Enums;

/// <summary>
/// The result of a rotation's optional session-termination step, reported alongside a resolved attempt.
/// </summary>
public enum PamSessionTerminationOutcome : byte
{
    /// <summary>The config's <see cref="Entities.PamRotationConfig.TerminateSessions"/> was false; termination was not attempted.</summary>
    NotRequested = 0,

    Terminated = 1,

    /// <summary>Termination was requested but failed; the rotation itself still succeeded.</summary>
    TermFailed = 2,
}

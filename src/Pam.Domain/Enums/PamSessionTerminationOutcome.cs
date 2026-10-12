namespace Bit.Pam.Enums;

/// <summary>The result of a rotation's optional session-termination step, reported with a successful attempt.</summary>
public enum PamSessionTerminationOutcome : byte
{
    /// <summary>The sealed terminate flag was false.</summary>
    NotRequested = 0,

    Terminated = 1,

    /// <summary>Termination failed; the rotation itself still succeeded.</summary>
    TermFailed = 2,
}

namespace Bit.Pam.Enums;

/// <summary>
/// The result of the guarded <c>PamRotationJob_Create</c> insert; each value is the code it returns.
/// </summary>
public enum PamRotationJobCreateOutcome
{
    Created = 1,

    /// <summary>
    /// The config already has a Pending or Claimed job, or a timed-out one the sweep has not recorded
    /// (<c>AtMostOneActiveJobPerConfig</c>). Nothing is persisted.
    /// </summary>
    ActiveJobExists = 0,

    /// <summary>
    /// The config is disabled, or its target is not automatic and active, when the insert runs. Nothing is persisted.
    /// </summary>
    ConfigNotOfferable = -1,
}

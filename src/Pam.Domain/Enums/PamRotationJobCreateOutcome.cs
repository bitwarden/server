namespace Bit.Pam.Enums;

/// <summary>
/// The result of the guarded <c>PamRotationJob_Create</c> insert (invariant <c>AtMostOneActiveJobPerConfig</c>,
/// enforced under <c>UPDLOCK, HOLDLOCK</c> on the config).
/// </summary>
public enum PamRotationJobCreateOutcome
{
    /// <summary>The job was inserted (stored proc returned 1).</summary>
    Created = 1,

    /// <summary>
    /// The config already has a Pending or Claimed job (stored proc returned 0) — the guard
    /// <c>AtMostOneActiveJobPerConfig</c> held against this insert. Nothing was persisted.
    /// </summary>
    ActiveJobExists = 0,

    /// <summary>
    /// The re-checked <c>can_offer</c> guard (enabled ∧ automatic ∧ target active) failed when the insert ran
    /// (stored proc returned -1). Nothing was persisted.
    /// </summary>
    ConfigNotOfferable = -1,
}

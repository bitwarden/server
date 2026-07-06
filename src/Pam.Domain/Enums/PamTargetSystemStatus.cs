namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamTargetSystem"/>. Only an <see cref="Active"/> target is offerable for
/// rotation (spec <c>can_offer</c>) or assignable to a daemon.
/// </summary>
public enum PamTargetSystemStatus : byte
{
    Active = 0,
    Disabled = 1,
}

namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamTargetSystem"/>. Only an <see cref="Active"/> target gets rotation jobs
/// offered or claimed.
/// </summary>
public enum PamTargetSystemStatus : byte
{
    Active = 0,
    Disabled = 1,
}

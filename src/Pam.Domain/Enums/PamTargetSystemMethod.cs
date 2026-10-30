namespace Bit.Pam.Enums;

/// <summary>
/// How a <see cref="Entities.PamTargetSystem"/> is rotated: by an access connector (<see cref="Automatic"/>), or by a
/// human out of band with PAM only tracking it (<see cref="Manual"/>).
/// </summary>
public enum PamTargetSystemMethod : byte
{
    Automatic = 0,
    Manual = 1,
}

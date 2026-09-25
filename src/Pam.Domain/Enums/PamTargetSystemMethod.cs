namespace Bit.Pam.Enums;

/// <summary>
/// How a <see cref="Entities.PamTargetSystem"/> is rotated: by an access connector (<see cref="Automatic"/>) or by a
/// human acting out of band (<see cref="Manual"/> — tracked by PAM but never executed by it).
/// </summary>
public enum PamTargetSystemMethod : byte
{
    Automatic = 0,
    Manual = 1,
}

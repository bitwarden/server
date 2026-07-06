namespace Bit.Pam.Enums;

/// <summary>
/// The connector an <see cref="PamTargetSystemMethod.Automatic"/> <see cref="Entities.PamTargetSystem"/> is rotated
/// through. Null on a <see cref="PamTargetSystemMethod.Manual"/> target, which has no connector.
/// </summary>
public enum PamTargetSystemKind : byte
{
    Entra = 0,
    Mssql = 1,
    CustomScript = 2,
}

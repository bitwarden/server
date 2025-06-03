namespace Bit.Core.Auth.Enums;

public enum EmergencyAccessType : byte
{
    /// <summary>
    /// Allows emergency contact to view the Grantor's data.
    /// </summary>
    View = 0,
    /// <summary>
    /// Allows emergency contact to take over the Grantor's account by overwriting the Grantor's password.
    /// </summary>
    Takeover = 1,
}

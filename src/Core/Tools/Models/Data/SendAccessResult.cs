namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// This enum represents the possible results when attempting to access a <see cref="Send"/>.
/// </summary>
public enum SendAccessResult
{
    Granted,
    PasswordRequired,
    PasswordInvalid,
    Denied
}

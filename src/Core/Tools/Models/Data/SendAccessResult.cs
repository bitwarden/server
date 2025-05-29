using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// This enum represents the possible results when attempting to access a <see cref="Send"/>.
/// </summary>
/// <member>name="Granted">Access is granted for the <see cref="Send"/>.</member>
/// <member>name="PasswordRequired">Access is denied, but a password is required to access the <see cref="Send"/>.
/// </member>
/// <member>name="PasswordInvalid">Access is denied due to an invalid password.</member>
/// <member>name="Denied">Access is denied for the <see cref="Send"/>.</member>
public enum SendAccessResult
{
    Granted,
    PasswordRequired,
    PasswordInvalid,
    Denied
}

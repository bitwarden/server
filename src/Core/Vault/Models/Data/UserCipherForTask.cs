// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// Minimal data model that represents a User and the associated cipher for a security task.
/// Only to be used for query responses. For full data model, <see cref="UserSecurityTaskCipher"/>.
/// </summary>
public class UserCipherForTask
{
    /// <summary>
    /// The user's Id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The user's email.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// The cipher Id of the security task.
    /// </summary>
    public Guid CipherId { get; set; }
}

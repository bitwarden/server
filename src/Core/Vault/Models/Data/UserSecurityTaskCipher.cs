namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// Data model that represents a User and the associated cipher for a security task.
/// </summary>
public class UserSecurityTaskCipher
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

namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// Data model that represents a User and the amount of actionable security tasks.
/// </summary>
public class UserSecurityTasksCount
{
    /// <summary>
    /// The User Id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The number of actionable security tasks for the respective users.
    /// </summary>
    public int TaskCount { get; set; }
}

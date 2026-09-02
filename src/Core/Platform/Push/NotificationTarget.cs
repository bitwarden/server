namespace Bit.Core.Platform.Push;

/// <summary>
/// Contains constants for all the available targets for a given notification.
/// </summary>
/// <remarks>
/// Please reach out to the Platform team if you need a new target added.
/// </remarks>
public enum NotificationTarget
{
    /// <summary>
    /// The target for the notification is a single user.
    /// </summary>
    User,
    /// <summary>
    /// The target for the notification are all the users in an organization.
    /// </summary>
    Organization,
    /// <summary>
    /// The target for the notification are all the organizations, 
    /// and all the users in that organization for a installation.
    /// </summary>
    Installation,
}

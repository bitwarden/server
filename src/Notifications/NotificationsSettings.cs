namespace Bit.Notifications;

/// <summary>
/// Settings for the Notifications service, bound from the <c>globalSettings</c> configuration section.
/// </summary>
public sealed class NotificationsSettings
{
    /// <summary>
    /// The Azure Storage connection string for the <c>notifications</c> queue. When empty, the queue
    /// is not read.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The Redis connection string used as the SignalR backplane. When empty, SignalR runs without a
    /// backplane.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// The id of this installation. When not empty, every connection joins the installation's groups.
    /// </summary>
    public Guid InstallationId { get; set; }
}

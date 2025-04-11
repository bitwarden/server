namespace Bit.Core.Settings;

public class NotificationHubPoolSettings
{
    /// <summary>
    /// List of Notification Hub settings to use for sending push notifications.
    ///
    /// Note that hubs on the same namespace share active device limits, so multiple namespaces should be used to increase capacity.
    /// </summary>
    public List<NotificationHubSettings> NotificationHubs { get; set; } = new();
}

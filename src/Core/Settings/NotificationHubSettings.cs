namespace Bit.Core.Settings;

public class NotificationHubSettings
{
    private string _connectionString;

    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value?.Trim('"');
    }
    public string HubName { get; set; }
    /// <summary>
    /// Enables TestSend on the Azure Notification Hub, which allows tracing of the request through the hub and to the platform-specific push notification service (PNS).
    /// Enabling this will result in delayed responses because the Hub must wait on delivery to the PNS.  This should ONLY be enabled in a non-production environment, as results are throttled.
    /// </summary>
    public bool EnableSendTracing { get; set; } = false;
    /// <summary>
    /// The date and time at which registration will be enabled.
    ///
    /// **This value should not be updated once set, as it is used to determine installation location of devices.**
    ///
    /// If null, registration is disabled.
    ///
    /// </summary>
    public DateTime? RegistrationStartDate { get; set; }
    /// <summary>
    /// The date and time at which registration will be disabled.
    ///
    /// **This value should not be updated once set, as it is used to determine installation location of devices.**
    ///
    /// If null, hub registration has no yet known expiry.
    /// </summary>
    public DateTime? RegistrationEndDate { get; set; }
}


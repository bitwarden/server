using System.Security.Cryptography;
using System.Text;
using System.Web;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Azure.NotificationHubs;

public class NotificationHubConnection
{
    public string HubName { get; init; }
    public string ConnectionString { get; init; }
    public Uri Endpoint => new NotificationHubConnectionStringBuilder(ConnectionString).Endpoint;
    private string SasKey => new NotificationHubConnectionStringBuilder(ConnectionString).SharedAccessKey;
    private string SasKeyName => new NotificationHubConnectionStringBuilder(ConnectionString).SharedAccessKeyName;
    public bool EnableSendTracing { get; init; }
    private NotificationHubClient _hubClient;
    /// <summary>
    /// Gets the NotificationHubClient for this connection.
    /// 
    /// If the client is null, it will be initialized.
    /// 
    /// <throws>Exception</throws> if the connection is invalid.
    /// </summary>
    public NotificationHubClient HubClient
    {
        get
        {
            if (_hubClient == null)
            {
                if (!IsValid)
                {
                    throw new Exception("Invalid notification hub settings");
                }
                Init();
            }
            return _hubClient;
        }
        private set
        {
            _hubClient = value;
        }
    }
    /// <summary>
    /// Gets the start date for registration.
    /// 
    /// If null, registration is always disabled.
    /// </summary>
    public DateTime? RegistrationStartDate { get; init; }
    /// <summary>
    /// Gets the end date for registration.
    /// 
    /// If null, registration has no end date.
    /// </summary>
    public DateTime? RegistrationEndDate { get; init; }
    /// <summary>
    /// Gets whether all data needed to generate a connection to Notification Hub is present.
    /// </summary>
    public bool IsValid
    {
        get
        {
            {
                var invalid = string.IsNullOrWhiteSpace(HubName) || string.IsNullOrWhiteSpace(ConnectionString);
                return !invalid;
            }
        }
    }

    /// <summary>
    /// Gets whether registration is enabled for the given comb ID.
    /// This is based off of the generation time encoded in the comb ID.
    /// </summary>
    /// <param name="comb"></param>
    /// <returns></returns>
    public bool RegistrationEnabled(Guid comb)
    {
        var combTime = CoreHelpers.DateFromComb(comb);
        return RegistrationEnabled(combTime);
    }

    /// <summary>
    /// Gets whether registration is enabled for the given time.
    /// </summary>
    /// <param name="queryTime">The time to check</param>
    /// <returns></returns>
    public bool RegistrationEnabled(DateTime queryTime)
    {
        if (queryTime >= RegistrationEndDate || RegistrationStartDate == null)
        {
            return false;
        }

        return RegistrationStartDate <= queryTime;
    }

    public HttpRequestMessage CreateRequest(HttpMethod method, string pathUri, params string[] queryParameters)
    {
        var uriBuilder = new UriBuilder(Endpoint)
        {
            Scheme = "https",
            Path = $"{HubName}/{pathUri.TrimStart('/')}",
            Query = string.Join('&', [.. queryParameters, "api-version=2015-01"]),
        };

        var result = new HttpRequestMessage(method, uriBuilder.Uri);
        result.Headers.Add("Authorization", generateSasToken(uriBuilder.Uri));
        result.Headers.Add("TrackingId", Guid.NewGuid().ToString());
        return result;
    }

    private string generateSasToken(Uri uri)
    {
        string targetUri = Uri.EscapeDataString(uri.ToString().ToLower()).ToLower();
        long expires = DateTime.UtcNow.AddMinutes(1).Ticks / TimeSpan.TicksPerSecond;
        string stringToSign = targetUri + "\n" + expires;

        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SasKey)))
        {
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            return $"SharedAccessSignature sr={targetUri}&sig={HttpUtility.UrlEncode(signature)}&se={expires}&skn={SasKeyName}";
        }
    }

    private NotificationHubConnection() { }

    /// <summary>
    /// Creates a new NotificationHubConnection from the given settings.
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public static NotificationHubConnection From(GlobalSettings.NotificationHubSettings settings)
    {
        return new()
        {
            HubName = settings.HubName,
            ConnectionString = settings.ConnectionString,
            EnableSendTracing = settings.EnableSendTracing,
            // Comb time is not precise enough for millisecond accuracy
            RegistrationStartDate = settings.RegistrationStart.HasValue ? Truncate(settings.RegistrationStart.Value, TimeSpan.FromMilliseconds(10)) : null,
            RegistrationEndDate = settings.RegistrationEnd
        };
    }

    private NotificationHubConnection Init()
    {
        HubClient = NotificationHubClient.CreateClientFromConnectionString(ConnectionString, HubName, EnableSendTracing);
        return this;
    }

    private static DateTime Truncate(DateTime dateTime, TimeSpan resolution)
    {
        return dateTime.AddTicks(-(dateTime.Ticks % resolution.Ticks));
    }
}

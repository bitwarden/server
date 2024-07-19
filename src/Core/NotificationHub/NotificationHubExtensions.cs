using System.Text.Json;
using Microsoft.Azure.NotificationHubs;

namespace Bit.Core.NotificationHub;

public static class NotificationHubExtensions
{
    public static string ToJson(this InstallationTemplate template)
    {
        return JsonSerializer.Serialize(template);
    }
}

using Azure;
using Azure.Data.Tables;

namespace Bit.Core.Models.Data;

public class InstallationDeviceEntity : ITableEntity
{
    public InstallationDeviceEntity() { }

    public InstallationDeviceEntity(Guid installationId, Guid deviceId)
    {
        PartitionKey = installationId.ToString();
        RowKey = deviceId.ToString();
    }

    public InstallationDeviceEntity(string prefixedDeviceId)
    {
        var parts = prefixedDeviceId.Split("_");
        if (parts.Length < 2)
        {
            throw new ArgumentException("Not enough parts.");
        }
        if (!Guid.TryParse(parts[0], out var installationId) || !Guid.TryParse(parts[1], out var deviceId))
        {
            throw new ArgumentException("Could not parse parts.");
        }
        PartitionKey = parts[0];
        RowKey = parts[1];
    }

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static bool IsInstallationDeviceId(string deviceId)
    {
        return deviceId != null && deviceId.Length == 73 && deviceId[36] == '_';
    }
    public static bool TryParse(string deviceId, out InstallationDeviceEntity installationDeviceEntity)
    {
        installationDeviceEntity = null;
        var installationId = Guid.Empty;
        var deviceIdGuid = Guid.Empty;
        if (!IsInstallationDeviceId(deviceId))
        {
            return false;
        }
        var parts = deviceId.Split("_");
        if (parts.Length < 2)
        {
            return false;
        }
        if (!Guid.TryParse(parts[0], out installationId) || !Guid.TryParse(parts[1], out deviceIdGuid))
        {
            return false;
        }
        installationDeviceEntity = new InstallationDeviceEntity(installationId, deviceIdGuid);
        return true;
    }
}

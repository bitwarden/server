using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Models.Data;

public class InstallationDeviceEntity : TableEntity
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

    public static bool IsInstallationDeviceId(string deviceId)
    {
        return deviceId != null && deviceId.Length == 73 && deviceId[36] == '_';
    }
}

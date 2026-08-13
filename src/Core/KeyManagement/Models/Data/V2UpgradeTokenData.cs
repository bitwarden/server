using System.Text.Json;

namespace Bit.Core.KeyManagement.Models.Data;

public class V2UpgradeTokenData
{
    public required string WrappedUserKey1 { get; init; }
    public required string WrappedUserKey2 { get; init; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static V2UpgradeTokenData? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<V2UpgradeTokenData>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

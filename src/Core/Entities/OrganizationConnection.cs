using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class OrganizationConnection<T> : OrganizationConnection where T : new()
{
    public new T Config
    {
        get => base.GetConfig<T>();
        set => base.SetConfig<T>(value);
    }
}

public class OrganizationConnection : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public OrganizationConnectionType Type { get; set; }
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public string Config { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public T GetConfig<T>() where T : new()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(Config);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void SetConfig<T>(T config) where T : new()
    {
        Config = JsonSerializer.Serialize(config);
    }
}

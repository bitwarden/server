using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class OrganizationConnection<T> : OrganizationConnection
    where T : IConnectionConfig
{
    [DisallowNull]
    public new T? Config
    {
        get => base.GetConfig<T>();
        set => base.SetConfig(value);
    }
}

public class OrganizationConnection : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public OrganizationConnectionType Type { get; set; }
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public string? Config { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public T? GetConfig<T>()
        where T : IConnectionConfig
    {
        try
        {
            if (Config is null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(Config);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void SetConfig<T>(T config)
        where T : IConnectionConfig
    {
        Config = JsonSerializer.Serialize(config);
    }

    public bool Validate<T>(out string exception)
        where T : IConnectionConfig
    {
        if (!Enabled)
        {
            exception = $"Connection disabled for organization {OrganizationId}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Config))
        {
            exception = $"No saved Connection config for organization {OrganizationId}";
            return false;
        }

        var config = GetConfig<T>();
        if (config == null)
        {
            exception = $"Error parsing Connection config for organization {OrganizationId}";
            return false;
        }

        return config.Validate(out exception);
    }
}

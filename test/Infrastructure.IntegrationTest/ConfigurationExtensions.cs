using Bit.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace Bit.Infrastructure.IntegrationTest;

public class Database
{
    public SupportedDatabaseProviders Type { get; set; }
    public string ConnectionString { get; set; } = default!;
    public bool UseEf { get; set; }
    public bool Enabled { get; set; } = true;
}

internal class TypedConfig
{
    public Database[] Databases { get; set; } = default!;
}

public static class ConfigurationExtensions
{
    public static Database[] GetDatabases(this IConfiguration config)
    {
        var typedConfig = config.Get<TypedConfig>();
        if (typedConfig.Databases == null)
        {
            return Array.Empty<Database>();
        }

        return typedConfig.Databases.Where(d => d.Enabled).ToArray();
    }
}

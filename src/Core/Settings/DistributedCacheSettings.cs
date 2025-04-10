namespace Bit.Core.Settings;

public class DistributedCacheSettings
{
    public virtual IConnectionStringSettings Redis { get; set; } = new ConnectionStringSettings();
    public virtual IConnectionStringSettings Cosmos { get; set; } = new ConnectionStringSettings();
}


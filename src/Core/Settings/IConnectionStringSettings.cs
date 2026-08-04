namespace Bit.Core.Settings;

public interface IConnectionStringSettings
{
    string ConnectionString { get; set; }
    string ServiceUri { get; set; }
}

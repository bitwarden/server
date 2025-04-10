namespace Bit.Core.Settings;

public class ConnectionStringSettings : IConnectionStringSettings
{
    private string _connectionString;

    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value.Trim('"');
    }
}

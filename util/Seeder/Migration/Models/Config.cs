namespace Bit.Seeder.Migration.Models;

public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Driver { get; set; }
}

public class CsvSettings
{
    public string OutputDir { get; set; } = "./exports";
    public string Delimiter { get; set; } = ",";
    public string Quoting { get; set; } = "QUOTE_ALL";
    public string Encoding { get; set; } = "utf-8";
    public bool IncludeHeaders { get; set; } = true;
    public string FallbackDelimiter { get; set; } = "|";
}

public class SSHTunnelConfig
{
    public bool Enabled { get; set; } = false;
    public string RemoteHost { get; set; } = string.Empty;
    public string RemoteUser { get; set; } = string.Empty;
    public int LocalPort { get; set; } = 1433;
    public int RemotePort { get; set; } = 1433;
    public string PrivateKeyPath { get; set; } = "~/.ssh/id_ed25519";
    public string? PrivateKeyPassphrase { get; set; }
}

public class MigrationConfig
{
    public DatabaseConfig? Source { get; set; }
    public Dictionary<string, DatabaseConfig> Destinations { get; set; } = new();
    public Dictionary<string, string> TableMappings { get; set; } = new();
    public Dictionary<string, List<string>> SpecialColumns { get; set; } = new();
    public List<string> ExcludeTables { get; set; } = new();
    public SSHTunnelConfig SshTunnel { get; set; } = new();
    public CsvSettings CsvSettings { get; set; } = new();
    public int BatchSize { get; set; } = 1000;
}

public class TableInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public Dictionary<string, string> ColumnTypes { get; set; } = new();
    public int RowCount { get; set; }
}

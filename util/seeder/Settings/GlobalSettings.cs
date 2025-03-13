namespace Bit.Seeder.Settings;

public class GlobalSettings
{
    public bool SelfHosted { get; set; }
    public string DatabaseProvider { get; set; } = string.Empty;
    public SqlSettings SqlServer { get; set; } = new SqlSettings();
    public SqlSettings PostgreSql { get; set; } = new SqlSettings();
    public SqlSettings MySql { get; set; } = new SqlSettings();
    public SqlSettings Sqlite { get; set; } = new SqlSettings();

    public class SqlSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
} 
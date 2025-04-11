namespace Bit.Core.Settings;

public class SqlSettings
{
    private string _connectionString;
    private string _readOnlyConnectionString;
    private string _jobSchedulerConnectionString;
    public bool SkipDatabasePreparation { get; set; }
    public bool DisableDatabaseMaintenanceJobs { get; set; }

    public string ConnectionString
    {
        get => _connectionString;
        set
        {
            // On development environment, the self-hosted overrides would not override the read-only connection string, since it is already set from the non-self-hosted connection string.
            // This causes a bug, where the read-only connection string is pointing to self-hosted database.
            if (!string.IsNullOrWhiteSpace(_readOnlyConnectionString) &&
                _readOnlyConnectionString == _connectionString)
            {
                _readOnlyConnectionString = null;
            }

            _connectionString = value.Trim('"');
        }
    }

    public string ReadOnlyConnectionString
    {
        get => string.IsNullOrWhiteSpace(_readOnlyConnectionString) ?
            _connectionString : _readOnlyConnectionString;
        set => _readOnlyConnectionString = value.Trim('"');
    }

    public string JobSchedulerConnectionString
    {
        get => _jobSchedulerConnectionString;
        set => _jobSchedulerConnectionString = value.Trim('"');
    }
}


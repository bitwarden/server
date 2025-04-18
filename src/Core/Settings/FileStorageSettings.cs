namespace Bit.Core.Settings;

public class FileStorageSettings : IFileStorageSettings
{
    private readonly GlobalSettings _globalSettings;
    private readonly string _urlName;
    private readonly string _directoryName;
    private string _connectionString;
    private string _baseDirectory;
    private string _baseUrl;

    public FileStorageSettings(GlobalSettings globalSettings, string urlName, string directoryName)
    {
        _globalSettings = globalSettings;
        _urlName = urlName;
        _directoryName = directoryName;
    }

    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value.Trim('"');
    }

    public string BaseDirectory
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildDirectory(_baseDirectory, string.Concat("/core/", _directoryName));
        set => _baseDirectory = value;
    }

    public string BaseUrl
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildExternalUri(_baseUrl, _urlName);
        set => _baseUrl = value;
    }
}

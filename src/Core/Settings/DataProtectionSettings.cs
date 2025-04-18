namespace Bit.Core.Settings;

public class DataProtectionSettings
{
    private readonly GlobalSettings _globalSettings;

    private string _directory;

    public DataProtectionSettings(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public string CertificateThumbprint { get; set; }
    public string CertificatePassword { get; set; }
    public string Directory
    {
        get => _globalSettings.InfrastructureResourceProvider.BuildDirectory(_directory, "/core/aspnet-dataprotection");
        set => _directory = value;
    }
}

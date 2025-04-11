namespace Bit.Core.Settings;

public class InstallationSettings : IInstallationSettings
{
    private string _identityUri;
    private string _apiUri;

    public Guid Id { get; set; }
    public string Key { get; set; }
    public string IdentityUri
    {
        get => string.IsNullOrWhiteSpace(_identityUri) ? "https://identity.bitwarden.com" : _identityUri;
        set => _identityUri = value;
    }
    public string ApiUri
    {
        get => string.IsNullOrWhiteSpace(_apiUri) ? "https://api.bitwarden.com" : _apiUri;
        set => _apiUri = value;
    }

}

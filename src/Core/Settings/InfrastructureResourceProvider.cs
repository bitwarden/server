namespace Bit.Core.Settings;

public class InfrastructureResourceProvider : IInfrastructureResourceProvider
{
    private readonly GlobalSettings _globalSettings;

    public InfrastructureResourceProvider(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public string BuildExternalUri(string explicitValue, string name)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!_globalSettings.SelfHosted)
        {
            return null;
        }
        return string.Format("{0}/{1}", _globalSettings.BaseServiceUri.Vault, name);
    }

    public string BuildInternalUri(string explicitValue, string name)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!_globalSettings.SelfHosted)
        {
            return null;
        }
        return string.Format("http://{0}:5000", name);
    }

    public string BuildDirectory(string explicitValue, string appendedPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!_globalSettings.SelfHosted)
        {
            return null;
        }
        return string.Concat("/etc/bitwarden", appendedPath);
    }
}

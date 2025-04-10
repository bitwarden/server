namespace Bit.Core.Settings;

public interface IInfrastructureResourceProvider
{
    string BuildExternalUri(string explicitValue, string name);
    string BuildInternalUri(string explicitValue, string name);
    string BuildDirectory(string explicitValue, string appendedPath);
}

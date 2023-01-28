using System.Reflection;

namespace Bit.Core.Utilities;

public static class AssemblyHelpers
{
    private static readonly IEnumerable<AssemblyMetadataAttribute> _assemblyMetadataAttributes;
    private static readonly AssemblyInformationalVersionAttribute _assemblyInformationalVersionAttributes;
    private const string GIT_HASH_ASSEMBLY_KEY = "GitHash";
    private static string _version;
    private static string _gitHash;

    static AssemblyHelpers()
    {
        _assemblyMetadataAttributes = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();
        _assemblyInformationalVersionAttributes = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    }

    public static string GetVersion()
    {
        if (string.IsNullOrWhiteSpace(_version))
        {
            _version = _assemblyInformationalVersionAttributes.InformationalVersion;
        }

        return _version;
    }

    public static string GetGitHash()
    {
        if (string.IsNullOrWhiteSpace(_gitHash))
        {
            try
            {
                _gitHash = _assemblyMetadataAttributes.Where(i => i.Key == GIT_HASH_ASSEMBLY_KEY).First().Value;
            }
            catch (Exception)
            { }
        }

        return _gitHash;
    }
}

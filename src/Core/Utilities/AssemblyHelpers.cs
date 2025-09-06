using System.Diagnostics;
using System.Reflection;

namespace Bit.Core.Utilities;

public static class AssemblyHelpers
{
    private static string? _version;
    private static string? _gitHash;

    static AssemblyHelpers()
    {
        var assemblyInformationalVersionAttribute = typeof(AssemblyHelpers).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (assemblyInformationalVersionAttribute == null)
        {
            Debug.Fail("");
            return;
        }

        var informationalVersion = assemblyInformationalVersionAttribute.InformationalVersion.AsSpan();

        if (!informationalVersion.TrySplitBy('+', out var version, out var gitHash))
        {
            // Treat the whole tbing as the version
            _version = informationalVersion.ToString();
            return;
        }

        _version = version.ToString();
        if (gitHash.Length < 8)
        {
            return;
        }
        _gitHash = gitHash[..8].ToString();
    }

    public static string? GetVersion()
    {
        return _version;
    }

    public static string? GetGitHash()
    {
        return _gitHash;
    }
}

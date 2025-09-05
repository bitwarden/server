using System.Diagnostics;
using System.Reflection;

namespace Bit.Core.Utilities;

public static class AssemblyHelpers
{
    private static string _version;
    private static string _gitHash;

    static AssemblyHelpers()
    {
        var assemblyInformationalVersionAttribute = typeof(AssemblyHelpers).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Debug.Assert(assemblyInformationalVersionAttribute is not null);

        var success = assemblyInformationalVersionAttribute.InformationalVersion.AsSpan().TrySplitBy('+', out var version, out var gitHash);
        Debug.Assert(success);

        _version = version.ToString();
        _gitHash = gitHash[..8].ToString();
    }

    public static string GetVersion()
    {
        return _version;
    }

    public static string GetGitHash()
    {
        return _gitHash;
    }
}

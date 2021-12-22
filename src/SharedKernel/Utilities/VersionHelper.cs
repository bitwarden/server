using System.Reflection;

namespace Bit.SharedKernel.Utilities
{
    public static class VersionHelper
    {
        private static string? _version;

        public static string GetVersion()
        {
            if (string.IsNullOrWhiteSpace(_version))
            {
                _version = Assembly.GetEntryAssembly()!
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                    .InformationalVersion;
            }

            return _version;
        }
    }
}

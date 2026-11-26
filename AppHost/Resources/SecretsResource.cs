using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that copies <c>dev/secrets.json</c> into each project's user secrets before the Bitwarden
/// services start, and loads the same file.
/// </summary>
internal static class SecretsResource
{
    private const string _secretsFileName = "secrets.json";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Configures the secrets setup executable resource.
        /// </summary>
        /// <returns>>The configured resource builder for the secrets setup executable.</returns>
        internal IResourceBuilder<ExecutableResource> ConfigureSecrets()
        {
            var settings = builder.Settings;
            return builder
                .AddExecutable("setup-secrets", "pwsh", settings.WorkingDirectory, "-File",
                    settings.Scripts.SecretsSetup, "-clear")
                .ExcludeFromManifest();
        }

        /// <summary>
        /// Loads <c>secrets.json</c> from <c>WorkingDirectory</c>, the file the secrets setup script copies.
        /// </summary>
        internal DevSecretsFile LoadDevSecrets()
        {
            var settings = builder.Settings;
            return DevSecretsFile.Load(
                Path.GetFullPath(Path.Combine(builder.AppHostDirectory, settings.WorkingDirectory, _secretsFileName)));
        }
    }
}

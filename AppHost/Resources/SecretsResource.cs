namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that copies <c>dev/secrets.json</c> into each project's user secrets before the Bitwarden
/// services start.
/// </summary>
public static class SecretsResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Configures the secrets setup executable resource.
        /// </summary>
        /// <returns>>The configured resource builder for the secrets setup executable.</returns>
        public IResourceBuilder<ExecutableResource> ConfigureSecrets()
        {
            return builder
                .AddExecutable("setup-secrets", "pwsh", "../dev", "-File", builder.Required("Scripts:SecretsSetup"),
                    "-clear")
                .ExcludeFromManifest();
        }
    }
}

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that copies <c>dev/secrets.json</c> into each project's user secrets before the Bitwarden
/// services start.
/// </summary>
public static class SecretsResource
{
    /// <summary>
    /// Configures the secrets setup executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure the secrets setup resource.</param>
    /// <returns>>The configured resource builder for the secrets setup executable.</returns>
    public static IResourceBuilder<ExecutableResource> ConfigureSecrets(this IDistributedApplicationBuilder builder)
    {
        return builder
            .AddExecutable("setup-secrets", "pwsh", "../dev", "-File", builder.Required("Scripts:SecretsSetup"),
                "-clear")
            .ExcludeFromManifest();
    }
}

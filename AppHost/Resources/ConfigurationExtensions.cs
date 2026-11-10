namespace Bit.AppHost.Resources;

/// <summary>
/// Reads and validates the AppHost configuration values that the resource definitions depend on.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Retrieves a required configuration value and throws an exception if it's missing.
    /// </summary>
    /// <param name="builder"> An instance of <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="key"> The configuration key to retrieve.</param>
    /// <returns> The configuration value associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal static string Required(this IDistributedApplicationBuilder builder, string key) =>
        builder.Configuration[key] ?? throw new InvalidOperationException($"Missing required configuration: {key}");

    /// <summary>
    /// Retrieves a required configuration value as an integer, throwing when it is missing or not a valid integer.
    /// </summary>
    internal static int RequiredInt(this IDistributedApplicationBuilder builder, string key)
    {
        if (!int.TryParse(builder.Required(key), out var value))
        {
            throw new InvalidOperationException($"Invalid value for {key}.");
        }

        return value;
    }

    /// <summary>
    /// Reads an image reference from configuration in <c>name[:tag]</c> form and returns its components,
    /// defaulting the tag to <c>latest</c> when not specified.
    /// </summary>
    internal static (string Name, string Tag) GetImageParts(this IDistributedApplicationBuilder builder, string key)
    {
        var parts = builder.Required(key).Split(':');
        return (parts[0], parts.Length > 1 ? parts[1] : "latest");
    }

    /// <summary>
    /// Determines if the application is running in self-hosted mode.
    /// </summary>
    /// <param name="builder"> An instance of <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <returns> True if the application is self-hosted, otherwise false.</returns>
    internal static bool IsSelfHosted(this IDistributedApplicationBuilder builder) =>
        builder.Configuration["SelfHost"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    internal static int GetBitwardenServicePort(this IDistributedApplicationBuilder builder, string serviceName)
    {
        var basePort = builder.RequiredInt($"Services:{serviceName}:BasePort");
        return builder.IsSelfHosted() ? basePort + 1 : basePort;
    }
}

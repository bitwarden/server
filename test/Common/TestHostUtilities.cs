#nullable enable

using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestHostUtilities
{
    /// <summary>
    /// Adds a few services that are core and can always be assumed for any Bitwarden service to inject.
    /// <list type="bullet">
    /// <item><see cref="IConfiguration"/></item>
    /// <item><see cref="GlobalSettings"/></item>
    /// <item><see cref="IHostEnvironment"/></item>
    /// <item><see cref="IWebHostEnvironment"/></item>
    /// <item><see cref="ILogger{T}"/></item>
    /// <item><see cref="ILoggerFactory"/></item>
    /// </list>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> that the services should be added to.</param>
    /// <param name="initialData">Initial data to seed <see cref="IConfiguration"> with.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTestHostServices(this IServiceCollection services, Dictionary<string, string?>? initialData = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        var globalSettings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(globalSettings);

        var environment = new TestEnvironment
        {
            EnvironmentName = config["Environment"] ?? "Production",
            ContentRootPath = config["ContentRoot"] ?? string.Empty,
            WebRootPath = config["WebRoot"] ?? string.Empty,
        };

        services.TryAddSingleton<IHostEnvironment>(environment);
        services.TryAddSingleton<IWebHostEnvironment>(environment);
        services.TryAddSingleton<IConfiguration>(config);
        services.TryAddSingleton(globalSettings);
        services.AddLogging();

        return services;
    }
    
    private class TestEnvironment : IHostEnvironment, IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public required string ContentRootPath { get; set; }
        public required string EnvironmentName { get; set; }
        public required string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

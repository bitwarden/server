using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.HttpExtensions;

public static class EndpointDataSourceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <paramref name="configure"/> so the offline OpenAPI generator (<c>dotnet swagger tofile</c>) can
    /// discover Minimal API endpoints it would otherwise miss — it never runs the <c>Configure</c> pipeline where
    /// the endpoints are mapped. Multiple features may call this; a single <see cref="StandaloneEndpointDataSource"/>
    /// composes all of their mappings, because ApiExplorer injects only one <see cref="EndpointDataSource"/> and a
    /// source-per-feature would let the last-registered hide the rest.
    ///
    /// Intentionally a no-op outside of swagger generation: at runtime the endpoints are served by the host's
    /// <c>UseEndpoints</c> mapping, and registering this source then would replace the default composite
    /// <see cref="EndpointDataSource"/> that runtime routing and link generation depend on.
    /// </summary>
    public static IServiceCollection AddOpenApiEndpointDataSource(
        this IServiceCollection services, Action<IEndpointRouteBuilder> configure)
    {
        // Set by dev/generate_openapi_files.ps1 (and the CI spec-generation job) while running the CLI generator.
        var generatingOpenApi = string.Equals(
            Environment.GetEnvironmentVariable("swaggerGen"), "true", StringComparison.OrdinalIgnoreCase);
        if (!generatingOpenApi)
        {
            return services;
        }

        // Register the composing data source once, on the first feature. Its factory runs only when ApiExplorer
        // resolves EndpointDataSource — by then every feature has registered its delegate, so the single instance
        // sees them all. The plain AddSingleton appends, so it wins over any framework-default EndpointDataSource.
        var firstFeature = services.All(d => d.ServiceType != typeof(OpenApiEndpointRouteConfiguration));
        services.AddSingleton(new OpenApiEndpointRouteConfiguration(configure));
        if (firstFeature)
        {
            services.AddSingleton<EndpointDataSource>(sp => new StandaloneEndpointDataSource(
                sp, sp.GetServices<OpenApiEndpointRouteConfiguration>().Select(c => c.Configure)));
        }

        return services;
    }

    /// <summary>
    /// Wraps a feature's endpoint-mapping delegate so the set of features can be enumerated from DI without
    /// registering a bare <see cref="Action{T}"/>, which would risk colliding with unrelated delegate registrations.
    /// </summary>
    private sealed class OpenApiEndpointRouteConfiguration(Action<IEndpointRouteBuilder> configure)
    {
        public Action<IEndpointRouteBuilder> Configure { get; } = configure;
    }
}

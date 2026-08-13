using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Bit.HttpExtensions;

/// <summary>
/// An <see cref="EndpointDataSource"/> that materializes Minimal API endpoints from one or more route-mapping
/// delegates outside the request pipeline.
///
/// Hosts that use the Startup/<c>Configure</c> pattern map their Minimal API endpoints with <c>UseEndpoints</c>.
/// The offline OpenAPI generator (<c>dotnet swagger tofile</c>) never executes <c>Configure</c>, so those
/// endpoints are invisible to ApiExplorer/Swashbuckle and the generated spec omits them. Registering this source
/// in DI makes the same endpoints discoverable without the request pipeline, while <c>UseEndpoints</c> still picks
/// them up at runtime.
///
/// A single instance composes every feature's mapping delegate: ApiExplorer injects one
/// <see cref="EndpointDataSource"/>, so a source-per-feature would let the last-registered hide the rest. See
/// <see cref="EndpointDataSourceServiceCollectionExtensions.AddOpenApiEndpointDataSource"/>.
/// </summary>
internal sealed class StandaloneEndpointDataSource : EndpointDataSource
{
    private readonly EndpointDataSource _source;

    public StandaloneEndpointDataSource(
        IServiceProvider serviceProvider, IEnumerable<Action<IEndpointRouteBuilder>> configure)
    {
        var routeBuilder = new StandaloneEndpointRouteBuilder(serviceProvider);
        foreach (var map in configure)
        {
            map(routeBuilder);
        }

        _source = new CompositeEndpointDataSource(routeBuilder.DataSources);
    }

    public override IReadOnlyList<Endpoint> Endpoints => _source.Endpoints;

    public override IChangeToken GetChangeToken() => _source.GetChangeToken();

    /// <summary>
    /// Minimal <see cref="IEndpointRouteBuilder"/> used only to materialize route groups into endpoints outside
    /// of the request pipeline.
    /// </summary>
    private sealed class StandaloneEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}

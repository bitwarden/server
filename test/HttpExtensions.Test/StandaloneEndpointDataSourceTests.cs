using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class StandaloneEndpointDataSourceTests
{
    [Fact]
    public void Endpoints_ComposesEveryMappingDelegate()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        Action<IEndpointRouteBuilder> mapA = b => b.DataSources.Add(new StubEndpointDataSource(EndpointTestData.Make("A")));
        Action<IEndpointRouteBuilder> mapB = b => b.DataSources.Add(new StubEndpointDataSource(EndpointTestData.Make("B")));

        var source = new StandaloneEndpointDataSource(serviceProvider, [mapA, mapB]);

        Assert.Equal(2, source.Endpoints.Count);
        Assert.Contains(source.Endpoints, e => e.DisplayName == "A");
        Assert.Contains(source.Endpoints, e => e.DisplayName == "B");
    }

    [Fact]
    public void Endpoints_NoDelegates_IsEmpty()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var source = new StandaloneEndpointDataSource(serviceProvider, []);

        Assert.Empty(source.Endpoints);
    }

    [Fact]
    public void GetChangeToken_ReturnsNonNullToken()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var source = new StandaloneEndpointDataSource(serviceProvider, []);

        Assert.NotNull(source.GetChangeToken());
    }

    [Fact]
    public void Endpoints_MaterializesMappedMinimalApiEndpoints()
    {
        // End-to-end against real Minimal API mapping (not just stub data sources): the delegate maps a
        // route, and StandaloneEndpointDataSource must surface it as a RouteEndpoint outside the request pipeline.
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddRouting()
            .BuildServiceProvider();
        Action<IEndpointRouteBuilder> map = b => b.MapGet("widgets/{id}", (string id) => id).WithName("Widgets_Get");

        var source = new StandaloneEndpointDataSource(serviceProvider, [map]);

        var endpoint = Assert.Single(source.Endpoints);
        var routeEndpoint = Assert.IsType<RouteEndpoint>(endpoint);
        Assert.Equal("widgets/{id}", routeEndpoint.RoutePattern.RawText);
    }
}

/// <summary>
/// Minimal <see cref="EndpointDataSource"/> exposing a fixed set of endpoints, used to drive the composition
/// logic of <see cref="StandaloneEndpointDataSource"/> without the Minimal API request-delegate machinery.
/// </summary>
internal sealed class StubEndpointDataSource(params Endpoint[] endpoints) : EndpointDataSource
{
    public override IReadOnlyList<Endpoint> Endpoints { get; } = endpoints;

    public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);
}

internal static class EndpointTestData
{
    public static Endpoint Make(string displayName) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(), displayName);
}

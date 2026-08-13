using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class EndpointDataSourceServiceCollectionExtensionsTests
{
    private const string SwaggerGenEnvVar = "swaggerGen";

    [Fact]
    public void AddOpenApiEndpointDataSource_WhenNotGenerating_IsNoOp()
    {
        using var _ = WithSwaggerGen(null);
        var services = new ServiceCollection();

        services.AddOpenApiEndpointDataSource(b => b.DataSources.Add(new StubEndpointDataSource(EndpointTestData.Make("A"))));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(EndpointDataSource));
    }

    [Fact]
    public void AddOpenApiEndpointDataSource_WhenGenerating_RegistersSingleSourceComposingAllFeatures()
    {
        using var _ = WithSwaggerGen("true");
        var services = new ServiceCollection();

        services.AddOpenApiEndpointDataSource(b => b.DataSources.Add(new StubEndpointDataSource(EndpointTestData.Make("A"))));
        services.AddOpenApiEndpointDataSource(b => b.DataSources.Add(new StubEndpointDataSource(EndpointTestData.Make("B"))));

        // A single EndpointDataSource is registered regardless of how many features register, otherwise
        // ApiExplorer (which injects only one) would surface just the last feature's endpoints.
        Assert.Single(services, d => d.ServiceType == typeof(EndpointDataSource));

        var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<EndpointDataSource>();

        Assert.Equal(2, source.Endpoints.Count);
        Assert.Contains(source.Endpoints, e => e.DisplayName == "A");
        Assert.Contains(source.Endpoints, e => e.DisplayName == "B");
    }

    /// <summary>
    /// Temporarily overrides the <c>swaggerGen</c> environment variable the extension gates on, restoring the
    /// previous value on dispose. Tests in this class run sequentially (single xUnit collection), so the
    /// process-wide variable does not race between them.
    /// </summary>
    private static IDisposable WithSwaggerGen(string? value)
    {
        var previous = Environment.GetEnvironmentVariable(SwaggerGenEnvVar);
        Environment.SetEnvironmentVariable(SwaggerGenEnvVar, value);
        return new EnvironmentVariableScope(SwaggerGenEnvVar, previous);
    }

    private sealed class EnvironmentVariableScope(string name, string? previousValue) : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable(name, previousValue);
    }
}

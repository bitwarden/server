using System.Reflection;

namespace Bit.Unified;

// TODO: Add way to map endpoints only
public interface IApplicationConfigurator
{
    Assembly AppAssembly { get; }
    string RoutePrefix { get; }
    void ConfigureServices(WebApplicationBuilder builder, IServiceCollection services);
    void Configure(WebApplication app, IApplicationBuilder builder);
}

public class ApplicationConfigurator<T> : IApplicationConfigurator
    where T : class
{
    private readonly T _instance;
    private readonly Action<T, WebApplicationBuilder, IServiceCollection> _configureServices;
    private readonly Action<T, WebApplication, IApplicationBuilder> _configure;

    public ApplicationConfigurator(
        string routePrefix,
        T instance,
        Action<T, WebApplicationBuilder, IServiceCollection> configureServices,
        Action<T, WebApplication, IApplicationBuilder> configure)
    {
        AppAssembly = typeof(T).Assembly;
        RoutePrefix = routePrefix;
        _instance = instance;
        _configureServices = configureServices;
        _configure = configure;
    }

    public Assembly AppAssembly { get; }
    public string RoutePrefix { get; }

    public void ConfigureServices(WebApplicationBuilder builder, IServiceCollection services)
    {
        _configureServices(_instance, builder, services);
    }

    public void Configure(WebApplication app, IApplicationBuilder builder)
    {
        _configure(_instance, app, builder);
    }
}

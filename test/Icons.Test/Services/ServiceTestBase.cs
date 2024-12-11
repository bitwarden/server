using Bit.Icons.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Icons.Test.Services;

public class ServiceTestBase
{
    internal ServiceCollection _services = new();
    internal ServiceProvider _provider;

    public ServiceTestBase()
    {
        _services = new ServiceCollection();
        _services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddDebug();
        });

        _services.ConfigureHttpClients();
        _services.AddHtmlParsing();
        _services.AddServices();

        _provider = _services.BuildServiceProvider();
    }

    public T GetService<T>() => _provider.GetRequiredService<T>();
}

public class ServiceTestBase<TSut> : ServiceTestBase
    where TSut : class
{
    public ServiceTestBase()
        : base()
    {
        _services.AddTransient<TSut>();
        _provider = _services.BuildServiceProvider();
    }

    public TSut Sut => GetService<TSut>();
}

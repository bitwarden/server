using AutoMapper;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SeederUtility.Configuration;

internal static class SeederServiceFactory
{
    internal static SeederServiceScope Create(SeederServiceOptions options)
    {
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: options.EnableMangling);
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new SeederServiceScope(provider, scope);
    }
}

internal record SeederServiceOptions
{
    internal required bool EnableMangling { get; init; }
}

internal sealed class SeederServiceScope : IDisposable
{
    internal DatabaseContext Db { get; }

    internal IMapper Mapper { get; }

    internal IPasswordHasher<User> PasswordHasher { get; }

    internal IManglerService Mangler { get; }

    internal SeederDependencies ToDependencies()
        => new(Db, Mapper, PasswordHasher, Mangler)
        {
            LicensingService = TryResolveLicensingService(),
            GlobalSettings = _scope.ServiceProvider.GetService<GlobalSettings>()
        };

    // LicensingService validates its certificate and (when self-hosted) requires a license directory
    // in its constructor, throwing if either is missing. Treat construction failure as "no licensing"
    // so a misconfigured environment still seeds non-premium data instead of crashing.
    private ILicensingService? TryResolveLicensingService()
    {
        try
        {
            return _scope.ServiceProvider.GetService<ILicensingService>();
        }
        catch
        {
            return null;
        }
    }

    private readonly ServiceProvider _provider;

    private readonly IServiceScope _scope;

    internal SeederServiceScope(ServiceProvider provider, IServiceScope scope)
    {
        _provider = provider;
        _scope = scope;
        var sp = scope.ServiceProvider;
        Db = sp.GetRequiredService<DatabaseContext>();
        Mapper = sp.GetRequiredService<IMapper>();
        PasswordHasher = sp.GetRequiredService<IPasswordHasher<User>>();
        Mangler = sp.GetRequiredService<IManglerService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

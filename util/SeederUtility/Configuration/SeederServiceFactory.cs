using AutoMapper;
using Bit.Core.Entities;
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
        => new(Db, Mapper, PasswordHasher, Mangler);

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

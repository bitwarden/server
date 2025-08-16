#nullable enable

using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Enums;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DbSeederUtility;

/// <summary>
/// Factory for creating configured service providers for seeder operations
/// </summary>
public static class ServiceProviderFactory
{
    private static IServiceProvider? _cachedProvider;
    private static SeederEnvironment? _cachedEnvironment;
    private static DatabaseProvider? _cachedDatabase;

    /// <summary>
    /// Gets or creates a service provider for the specified environment and database
    /// </summary>
    public static IServiceProvider GetServiceProvider(
        SeederEnvironment environment = SeederEnvironment.Auto,
        DatabaseProvider database = DatabaseProvider.Auto)
    {
        // Return cached provider if environment and database haven't changed
        if (_cachedProvider != null && _cachedEnvironment == environment && _cachedDatabase == database)
        {
            return _cachedProvider;
        }

        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, environment, database);

        _cachedProvider = services.BuildServiceProvider();
        _cachedEnvironment = environment;
        _cachedDatabase = database;

        return _cachedProvider;
    }

    /// <summary>
    /// Creates a new scope and returns the required services for seeding operations
    /// </summary>
    public static SeederServiceScope CreateScope(
        SeederEnvironment environment = SeederEnvironment.Auto,
        DatabaseProvider database = DatabaseProvider.Auto)
    {
        var provider = GetServiceProvider(environment, database);
        var scope = provider.CreateScope();

        return new SeederServiceScope(scope);
    }
}

/// <summary>
/// Encapsulates commonly used seeder services in a disposable scope
/// </summary>
public class SeederServiceScope : IDisposable
{
    private readonly IServiceScope _scope;

    public DatabaseContext Database { get; }
    public ISeederCryptoService CryptoService { get; }
    public IDataProtectionService DataProtection { get; }
    public IPasswordHasher<User> PasswordHasher { get; }

    internal SeederServiceScope(IServiceScope scope)
    {
        _scope = scope;
        var provider = scope.ServiceProvider;

        Database = provider.GetRequiredService<DatabaseContext>();
        CryptoService = provider.GetRequiredService<ISeederCryptoService>();
        DataProtection = provider.GetRequiredService<IDataProtectionService>();
        PasswordHasher = provider.GetRequiredService<IPasswordHasher<User>>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.DataMigrations;

public static class DataMigrationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data migration services, which use <typeparamref name="TContext"/>. Its model must include
    /// this assembly's entity configurations.
    /// </summary>
    public static IServiceCollection AddDataMigrations<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DataMigrationStateRepository>();
        services.TryAddSingleton<IDataMigrationStateRepository>(sp => sp.GetRequiredService<DataMigrationStateRepository>());
        services.AddMetrics();
        services.TryAddSingleton<DataMigrationMetrics>();
        services.TryAddSingleton<IDataMigrationRunner, DataMigrationRunner>();
        return services;
    }

    public static IServiceCollection AddDataMigration<T>(this IServiceCollection services) where T : DataMigration
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<DataMigration, T>());
        return services;
    }
}

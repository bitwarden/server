using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework;

public static class EntityFrameworkServiceCollectionExtensions
{
    public static void AddEFRepositories(this IServiceCollection services, bool selfHosted, string connectionString,
        SupportedDatabaseProviders provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Database provider type {provider} was selected but no connection string was found.");
        }
        LinqToDBForEFTools.Initialize();
        services.AddAutoMapper(typeof(UserRepository));
        services.AddDbContext<DatabaseContext>(options =>
        {
            if (provider == SupportedDatabaseProviders.Postgres)
            {
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly("PostgresMigrations"));
                // Handle NpgSql Legacy Support for `timestamp without timezone` issue
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            }
            else if (provider == SupportedDatabaseProviders.MySql)
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                    b => b.MigrationsAssembly("MySqlMigrations"));
            }
        });
        services.AddSingleton<ICipherRepository, CipherRepository>();
        services.AddSingleton<ICollectionCipherRepository, CollectionCipherRepository>();
        services.AddSingleton<ICollectionRepository, CollectionRepository>();
        services.AddSingleton<IDeviceRepository, DeviceRepository>();
        services.AddSingleton<IEmergencyAccessRepository, EmergencyAccessRepository>();
        services.AddSingleton<IFolderRepository, FolderRepository>();
        services.AddSingleton<IGrantRepository, GrantRepository>();
        services.AddSingleton<IGroupRepository, GroupRepository>();
        services.AddSingleton<IInstallationRepository, InstallationRepository>();
        services.AddSingleton<IMaintenanceRepository, MaintenanceRepository>();
        services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
        services.AddSingleton<IOrganizationApiKeyRepository, OrganizationApiKeyRepository>();
        services.AddSingleton<IOrganizationConnectionRepository, OrganizationConnectionRepository>();
        services.AddSingleton<IOrganizationSponsorshipRepository, OrganizationSponsorshipRepository>();
        services.AddSingleton<IOrganizationUserRepository, OrganizationUserRepository>();
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<ISendRepository, SendRepository>();
        services.AddSingleton<ISsoConfigRepository, SsoConfigRepository>();
        services.AddSingleton<ISsoUserRepository, SsoUserRepository>();
        services.AddSingleton<ITaxRateRepository, TaxRateRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IProviderRepository, ProviderRepository>();
        services.AddSingleton<IProviderUserRepository, ProviderUserRepository>();
        services.AddSingleton<IProviderOrganizationRepository, ProviderOrganizationRepository>();
        services.AddSingleton<IAuthRequestRepository, AuthRequestRepository>();

        if (selfHosted)
        {
            services.AddSingleton<IEventRepository, EventRepository>();
        }
    }
}

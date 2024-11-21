using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Billing.Repositories;
using Bit.Infrastructure.EntityFramework.NotificationCenter.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Repositories;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework;

public static class EntityFrameworkServiceCollectionExtensions
{
    public static void SetupEntityFramework(this IServiceCollection services, string connectionString, SupportedDatabaseProviders provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Database provider type {provider} was selected but no connection string was found.");
        }

        // TODO: We should move away from using LINQ syntax for EF (TDL-48).
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
            else if (provider == SupportedDatabaseProviders.Sqlite)
            {
                options.UseSqlite(connectionString, b => b.MigrationsAssembly("SqliteMigrations"));
            }
            else if (provider == SupportedDatabaseProviders.SqlServer)
            {
                options.UseSqlServer(connectionString);
            }
        });
    }

    public static void AddPasswordManagerEFRepositories(this IServiceCollection services, bool selfHosted)
    {
        services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
        services.AddSingleton<IAuthRequestRepository, AuthRequestRepository>();
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
        services.AddSingleton<IOrganizationApiKeyRepository, OrganizationApiKeyRepository>();
        services.AddSingleton<IOrganizationConnectionRepository, OrganizationConnectionRepository>();
        services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
        services.AddSingleton<IOrganizationSponsorshipRepository, OrganizationSponsorshipRepository>();
        services.AddSingleton<IOrganizationUserRepository, OrganizationUserRepository>();
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<IProviderOrganizationRepository, ProviderOrganizationRepository>();
        services.AddSingleton<IProviderRepository, ProviderRepository>();
        services.AddSingleton<IProviderUserRepository, ProviderUserRepository>();
        services.AddSingleton<ISendRepository, SendRepository>();
        services.AddSingleton<ISsoConfigRepository, SsoConfigRepository>();
        services.AddSingleton<ISsoUserRepository, SsoUserRepository>();
        services.AddSingleton<ITaxRateRepository, TaxRateRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IOrganizationDomainRepository, OrganizationDomainRepository>();
        services.AddSingleton<IWebAuthnCredentialRepository, WebAuthnCredentialRepository>();
        services.AddSingleton<IProviderPlanRepository, ProviderPlanRepository>();
        services.AddSingleton<IProviderInvoiceItemRepository, ProviderInvoiceItemRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<INotificationStatusRepository, NotificationStatusRepository>();
        services
            .AddSingleton<IClientOrganizationMigrationRecordRepository, ClientOrganizationMigrationRecordRepository>();
        services.AddSingleton<IPasswordHealthReportApplicationRepository, PasswordHealthReportApplicationRepository>();
        services.AddSingleton<ISecurityTaskRepository, SecurityTaskRepository>();

        if (selfHosted)
        {
            services.AddSingleton<IEventRepository, EventRepository>();
        }
    }
}

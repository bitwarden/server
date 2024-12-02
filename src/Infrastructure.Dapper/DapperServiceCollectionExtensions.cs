using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Repositories;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Tools.Repositories;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.AdminConsole.Repositories;
using Bit.Infrastructure.Dapper.Auth.Repositories;
using Bit.Infrastructure.Dapper.Billing.Repositories;
using Bit.Infrastructure.Dapper.KeyManagement.Repositories;
using Bit.Infrastructure.Dapper.NotificationCenter.Repositories;
using Bit.Infrastructure.Dapper.Platform;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Infrastructure.Dapper.SecretsManager.Repositories;
using Bit.Infrastructure.Dapper.Tools.Repositories;
using Bit.Infrastructure.Dapper.Vault.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.Dapper;

public static class DapperServiceCollectionExtensions
{
    public static void AddDapperRepositories(this IServiceCollection services, bool selfHosted)
    {
        services.AddSingleton<IApiKeyRepository, ApiKeyRepository>();
        services.AddSingleton<IAuthRequestRepository, AuthRequestRepository>();
        services.AddSingleton<ICipherRepository, CipherRepository>();
        services.AddSingleton<ICollectionCipherRepository, CollectionCipherRepository>();
        services.AddSingleton<ICollectionRepository, CollectionRepository>();
        services.AddSingleton<IDeviceRepository, DeviceRepository>();
        services.AddSingleton<IEmergencyAccessRepository, EmergencyAccessRepository>();
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
        services.AddSingleton<IUserAsymmetricKeysRepository, UserAsymmetricKeysRepository>();

        if (selfHosted)
        {
            services.AddSingleton<IEventRepository, EventRepository>();
        }
    }
}

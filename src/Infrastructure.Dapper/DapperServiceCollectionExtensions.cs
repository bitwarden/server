using Bit.Core.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.Dapper;

public static class DapperServiceCollectionExtensions
{
    public static void AddDapperRepositories(this IServiceCollection services, bool selfHosted)
    {
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
        services.AddSingleton<IOrganizationSponsorshipRepository, OrganizationSponsorshipRepository>();
        services.AddSingleton<IOrganizationUserRepository, OrganizationUserRepository>();
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<ISendRepository, SendRepository>();
        services.AddSingleton<ISsoConfigRepository, SsoConfigRepository>();
        services.AddSingleton<ISsoUserRepository, SsoUserRepository>();
        services.AddSingleton<ITaxRateRepository, TaxRateRepository>();
        services.AddSingleton<IEmergencyAccessRepository, EmergencyAccessRepository>();
        services.AddSingleton<IProviderRepository, ProviderRepository>();
        services.AddSingleton<IProviderUserRepository, ProviderUserRepository>();
        services.AddSingleton<IProviderOrganizationRepository, ProviderOrganizationRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IOrganizationApiKeyRepository, OrganizationApiKeyRepository>();
        services.AddSingleton<IOrganizationConnectionRepository, OrganizationConnectionRepository>();
        services.AddSingleton<IAuthRequestRepository, AuthRequestRepository>();

        if (selfHosted)
        {
            services.AddSingleton<IEventRepository, EventRepository>();
        }
    }
}

using Bit.Core.Vault.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Vault;

public static class VaultServiceCollectionExtensions
{
    public static IServiceCollection AddVaultServices(this IServiceCollection services)
    {
        services.AddVaultQueries();

        return services;
    }

    private static void AddVaultQueries(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationCiphersQuery, OrganizationCiphersQuery>();
    }
}

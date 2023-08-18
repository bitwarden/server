using Bit.Core.Vault.AuthorizationHandlers.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Vault;

public static class VaultServiceCollectionExtensions
{
    public static void AddVaultServices(this IServiceCollection services)
    {
        services.AddAuthorizationHandlers();
    }

    private static void AddAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CollectionAuthorizationHandler>();
    }   
}
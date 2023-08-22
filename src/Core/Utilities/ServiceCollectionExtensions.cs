using Bit.Core.Vault.AuthorizationHandlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCoreAuthorizationHandlers(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CollectionAccessAuthorizationHandler>();
    }
}

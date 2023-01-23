using Bit.Commercial.Core.SecretManagerFeatures;
using Bit.Commercial.Core.Services;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommercialCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, ProviderService>();
    }

    public static void AddCommercialSecretsManagerServices(this IServiceCollection services)
    {
        services.AddSecretManagerServices();
    }
}

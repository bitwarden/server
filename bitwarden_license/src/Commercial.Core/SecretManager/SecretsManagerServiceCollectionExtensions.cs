using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretManager;

public static class SecretsManagerServiceCollectionExtensions
{
    public static void AddCommercialSecretsManagerServices(this IServiceCollection services)
    {
        services.AddSecretManagerServices();
    }
}

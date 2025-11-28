using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.SecretsManager;

public static class SecretsManagerServiceCollectionExtensions
{
    public static void AddCommercialSecretsManagerServices(this IServiceCollection services)
    {
        services.AddSecretsManagerServices();
    }
}

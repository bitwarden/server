using Bit.Commercial.Core.SecretManagerFeatures;
using Bit.Commercial.Core.Services;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommCoreServices(this IServiceCollection services, bool api = false)
    {
        services.AddScoped<IProviderService, ProviderService>();
        if (api)
        {
            services.AddSecretManagerServices();
        }
    }
}

using Bit.Commercial.Core.Services;
using Bit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, ProviderService>();
    }
}

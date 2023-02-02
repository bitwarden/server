using Bit.Core.AdminFeatures.Providers;
using Bit.Core.AdminFeatures.Providers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.AdminFeatures;

public static class AdminServiceCollectionExtensions
{
    public static void AddProviderCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreateProviderCommand, CreateProviderCommand>();
    }
}

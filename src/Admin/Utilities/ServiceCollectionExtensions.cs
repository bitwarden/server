using Bit.Admin.Providers;
using Bit.Admin.Providers.Interfaces;

namespace Bit.Admin.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddProviderCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreateProviderCommand, CreateProviderCommand>();
    }
}

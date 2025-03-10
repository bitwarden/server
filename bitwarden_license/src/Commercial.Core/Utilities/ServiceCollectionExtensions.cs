using Bit.Commercial.Core.AdminConsole.Providers;
using Bit.Commercial.Core.AdminConsole.Services;
using Bit.Commercial.Core.Billing;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Core.Utilities;

public static class ServiceCollectionExtensions
{
    public static void AddCommercialCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<ICreateProviderCommand, CreateProviderCommand>();
        services.AddScoped<IRemoveOrganizationFromProviderCommand, RemoveOrganizationFromProviderCommand>();
        services.AddTransient<IProviderBillingService, ProviderBillingService>();
    }
}

using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public static class OrganizationSubscriptionServiceCollectionExtensions
{
    public static void AddOrganizationSubscriptionServices(this IServiceCollection services)
    {
        services.AddScoped<IUpdateSecretsManagerSubscriptionCommand, UpdateSecretsManagerSubscriptionCommand>();
        services.AddScoped<IUpgradeOrganizationPlanCommand, UpgradeOrganizationPlanCommand>();
        services.AddScoped<IAddSecretsManagerSubscriptionCommand, AddSecretsManagerSubscriptionCommand>();
    }
}

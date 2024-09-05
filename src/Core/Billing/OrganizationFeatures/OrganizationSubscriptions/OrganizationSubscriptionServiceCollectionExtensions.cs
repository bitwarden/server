using Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSubscriptions;

public static class OrganizationSubscriptionServiceCollectionExtensions
{
    public static void AddOrganizationSubscriptionServices(this IServiceCollection services)
    {
        services.AddScoped<IUpgradeOrganizationPlanCommand, UpgradeOrganizationPlanCommand>();
        services.AddScoped<IAddSecretsManagerSubscriptionCommand, AddSecretsManagerSubscriptionCommand>();
    }
}

using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public static class OrganizationSubscriptionServiceCollectionExtensions
{
    public static void AddOrganizationSubscriptionServices(this IServiceCollection services)
    {
        services.AddScoped<IUpgradeOrganizationPlanCommand, UpgradeOrganizationPlanCommand>();
    }
}

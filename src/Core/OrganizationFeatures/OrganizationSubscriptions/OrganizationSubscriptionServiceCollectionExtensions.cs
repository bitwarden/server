using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public static class OrganizationSubscriptionServiceCollectionExtensions
{
    public static void AddOrganizationSubscriptionServices(this IServiceCollection services)
    {
        services
            .AddScoped<IUpgradeOrganizationPlanCommand, UpgradeOrganizationPlanCommand>()
            .AddScoped<IAddSecretsManagerSubscriptionCommand, AddSecretsManagerSubscriptionCommand>()
            .AddScoped<IGetOrganizationSubscriptionsToUpdateQuery, GetOrganizationSubscriptionsToUpdateQuery>()
            .AddScoped<IUpdateOrganizationSubscriptionCommand, UpdateOrganizationSubscriptionCommand>();
    }
}

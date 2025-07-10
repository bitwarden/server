using Bit.Api.Billing.Queries.Organizations;

namespace Bit.Api.Billing;

public static class Registrations
{
    public static void AddBillingQueries(this IServiceCollection services)
    {
        services.AddTransient<IOrganizationWarningsQuery, OrganizationWarningsQuery>();
    }
}

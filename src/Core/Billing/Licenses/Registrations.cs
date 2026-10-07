using Bit.Core.Billing.Licenses.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Licenses;

public static class Registrations
{
    public static void AddLicenseOperations(this IServiceCollection services)
    {
        // Queries
        services.AddTransient<IGetUserLicenseQuery, GetUserLicenseQuery>();
    }
}

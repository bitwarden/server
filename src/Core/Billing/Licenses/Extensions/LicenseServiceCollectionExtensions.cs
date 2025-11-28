using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses.Services;
using Bit.Core.Billing.Licenses.Services.Implementations;
using Bit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseServiceCollectionExtensions
{
    public static void AddLicenseServices(this IServiceCollection services)
    {
        services.AddTransient<ILicenseClaimsFactory<Organization>, OrganizationLicenseClaimsFactory>();
        services.AddTransient<ILicenseClaimsFactory<User>, UserLicenseClaimsFactory>();
    }
}

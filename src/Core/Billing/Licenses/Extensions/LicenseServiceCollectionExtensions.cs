using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses.Services;
using Bit.Core.Billing.Licenses.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Licenses.Extensions;

public static class LicenseServiceCollectionExtensions
{
    public static void AddLicenseServices(this IServiceCollection services)
    {
        services.AddTransient<ILicenseClaimsFactory<Organization>, OrganizationLicenseClaimsFactory>();
    }
}

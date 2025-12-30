using Bit.Core.Billing.Licenses.Models.Api.Response;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Billing.Licenses.Queries;

public interface IGetUserLicenseQuery
{
    Task<LicenseResponseModel> Run(User user);
}

public class GetUserLicenseQuery(
    IUserService userService,
    ILicensingService licensingService) : IGetUserLicenseQuery
{
    public async Task<LicenseResponseModel> Run(User user)
    {
        var license = await userService.GenerateLicenseAsync(user);
        var claimsPrincipal = licensingService.GetClaimsPrincipalFromLicense(license);
        return new LicenseResponseModel(license, claimsPrincipal);
    }
}

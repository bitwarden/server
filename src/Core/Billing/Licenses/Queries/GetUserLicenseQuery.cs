using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Billing.Licenses.Queries;

public interface IGetUserLicenseQuery
{
    Task<UserLicense> Run(User user);
}

public class GetUserLicenseQuery(
    IUserService userService) : IGetUserLicenseQuery
{
    public async Task<UserLicense> Run(User user)
    {
        return await userService.GenerateLicenseAsync(user);
    }
}

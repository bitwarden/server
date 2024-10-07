namespace Bit.Core.Billing.Licenses.UserLicenses;

public interface IGetUserLicenseQueryHandler
{
    public Task<UserLicense> Handle(GetUserLicenseQuery query);
}

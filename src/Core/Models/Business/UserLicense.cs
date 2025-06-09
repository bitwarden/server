using System.Security.Claims;
using System.Text;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Models.Business;

public class UserLicense : BaseLicense
{
    public UserLicense()
    { }

    public UserLicense(User user, SubscriptionInfo subscriptionInfo, ILicensingService licenseService,
        int? version = null)
    {
        LicenseType = Enums.LicenseType.User;
        LicenseKey = user.LicenseKey;
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Version = version.GetValueOrDefault(1);
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Issued = DateTime.UtcNow;
        Expires = user.CalculateFreshExpirationDate(subscriptionInfo);
        Refresh = user.CalculateFreshRefreshDate(subscriptionInfo);
        Trial = user.IsTrialing(subscriptionInfo);

        Hash = Convert.ToBase64String(ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    public UserLicense(User user, ILicensingService licenseService, int? version = null)
    {
        LicenseType = Enums.LicenseType.User;
        LicenseKey = user.LicenseKey;
        Id = user.Id;
        Name = user.Name;
        Email = user.Email;
        Version = version.GetValueOrDefault(1);
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Issued = DateTime.UtcNow;
        Expires = user.CalculateFreshExpirationDate(null);
        Refresh = user.CalculateFreshRefreshDate(null);
        Trial = user.IsTrialing(null);

        Hash = Convert.ToBase64String(ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    [LicenseVersion(1)]
    public string Email { get; set; }

    [LicenseVersion(1)]
    public bool Premium { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    private bool ValidLicenseVersion
    {
        get => Version == 1;
    }

    public override byte[] GetDataBytes(bool forHash = false)
    {
        if (!ValidLicenseVersion)
        {
            throw new NotSupportedException($"Version {Version} is not supported.");
        }

        return this.GetDataBytesWithAttributes(forHash);
    }

    public bool CanUse(User user, ClaimsPrincipal claimsPrincipal, out string exception)
    {
        var errorMessages = new StringBuilder();

        if (!user.EmailVerified)
        {
            errorMessages.AppendLine("The user's email is not verified.");
        }

        var email = claimsPrincipal.GetValue<string>(nameof(Email));
        if (!email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            errorMessages.AppendLine("The user's email does not match the license email.");
        }

        if (errorMessages.Length > 0)
        {
            exception = $"Invalid license. {errorMessages.ToString().TrimEnd()}";
            return false;
        }

        exception = "";
        return true;
    }

    public bool VerifyData(User user, ClaimsPrincipal claimsPrincipal)
    {
        var licenseKey = claimsPrincipal.GetValue<string>(nameof(LicenseKey));
        var premium = claimsPrincipal.GetValue<bool>(nameof(Premium));
        var email = claimsPrincipal.GetValue<string>(nameof(Email));

        return licenseKey == user.LicenseKey &&
               premium == user.Premium &&
               email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase);
    }
}

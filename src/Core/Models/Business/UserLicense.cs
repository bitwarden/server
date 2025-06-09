using System.Text.Json.Serialization;
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

        Hash = Convert.ToBase64String(this.ComputeHash());
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

        Hash = Convert.ToBase64String(this.ComputeHash());
        Signature = Convert.ToBase64String(licenseService.SignLicense(this));
    }

    [LicenseVersion(1)]
    public string Email { get; set; }

    [LicenseVersion(1)]
    public bool Premium { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public override bool ValidLicenseVersion
    {
        get => Version == 1;
    }


}

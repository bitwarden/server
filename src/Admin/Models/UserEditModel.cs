using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;

namespace Bit.Admin.Models;

public class UserEditModel
{
    public UserEditModel() { }

    public UserEditModel(
        User user,
        bool isTwoFactorEnabled,
        IEnumerable<Cipher> ciphers,
        BillingInfo billingInfo,
        BillingHistoryInfo billingHistoryInfo,
        GlobalSettings globalSettings,
        bool? claimedAccount)
    {
        User = UserViewModel.MapViewModel(user, isTwoFactorEnabled, ciphers, claimedAccount);

        BillingInfo = billingInfo;
        BillingHistoryInfo = billingHistoryInfo;
        BraintreeMerchantId = globalSettings.Braintree.MerchantId;

        Name = user.Name;
        Email = user.Email;
        EmailVerified = user.EmailVerified;
        Premium = user.Premium;
        MaxStorageGb = user.MaxStorageGb;
        Gateway = user.Gateway;
        GatewayCustomerId = user.GatewayCustomerId;
        GatewaySubscriptionId = user.GatewaySubscriptionId;
        LicenseKey = user.LicenseKey;
        PremiumExpirationDate = user.PremiumExpirationDate;
    }

    public UserViewModel User { get; init; }
    public BillingInfo BillingInfo { get; init; }
    public BillingHistoryInfo BillingHistoryInfo { get; init; }
    public string RandomLicenseKey => CoreHelpers.SecureRandomString(20);
    public string OneYearExpirationDate => DateTime.Now.AddYears(1).ToString("yyyy-MM-ddTHH:mm");
    public string BraintreeMerchantId { get; init; }

    [Display(Name = "Name")]
    public string Name { get; init; }
    [Required]
    [Display(Name = "Email")]
    public string Email { get; init; }
    [Display(Name = "Email Verified")]
    public bool EmailVerified { get; init; }
    [Display(Name = "Premium")]
    public bool Premium { get; init; }
    [Display(Name = "Max. Storage GB")]
    public short? MaxStorageGb { get; init; }
    [Display(Name = "Gateway")]
    public Core.Enums.GatewayType? Gateway { get; init; }
    [Display(Name = "Gateway Customer Id")]
    public string GatewayCustomerId { get; init; }
    [Display(Name = "Gateway Subscription Id")]
    public string GatewaySubscriptionId { get; init; }
    [Display(Name = "License Key")]
    public string LicenseKey { get; init; }
    [Display(Name = "Premium Expiration Date")]
    public DateTime? PremiumExpirationDate { get; init; }
}

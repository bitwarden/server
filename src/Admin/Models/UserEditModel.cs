using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Admin.Models;

public class UserEditModel : UserViewModel
{
    public UserEditModel() { }

    public UserEditModel(User user, IEnumerable<Cipher> ciphers, BillingInfo billingInfo,
        GlobalSettings globalSettings)
        : base(user, ciphers)
    {
        BillingInfo = billingInfo;
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

    public BillingInfo BillingInfo { get; set; }
    public string RandomLicenseKey => CoreHelpers.SecureRandomString(20);
    public string OneYearExpirationDate => DateTime.Now.AddYears(1).ToString("yyyy-MM-ddTHH:mm");
    public string BraintreeMerchantId { get; set; }

    [Display(Name = "Name")]
    public string Name { get; set; }
    [Required]
    [Display(Name = "Email")]
    public string Email { get; set; }
    [Display(Name = "Email Verified")]
    public bool EmailVerified { get; set; }
    [Display(Name = "Premium")]
    public bool Premium { get; set; }
    [Display(Name = "Max. Storage GB")]
    public short? MaxStorageGb { get; set; }
    [Display(Name = "Gateway")]
    public Core.Enums.GatewayType? Gateway { get; set; }
    [Display(Name = "Gateway Customer Id")]
    public string GatewayCustomerId { get; set; }
    [Display(Name = "Gateway Subscription Id")]
    public string GatewaySubscriptionId { get; set; }
    [Display(Name = "License Key")]
    public string LicenseKey { get; set; }
    [Display(Name = "Premium Expiration Date")]
    public DateTime? PremiumExpirationDate { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.Name = Name;
        existingUser.Email = Email;
        existingUser.EmailVerified = EmailVerified;
        existingUser.Premium = Premium;
        existingUser.MaxStorageGb = MaxStorageGb;
        existingUser.Gateway = Gateway;
        existingUser.GatewayCustomerId = GatewayCustomerId;
        existingUser.GatewaySubscriptionId = GatewaySubscriptionId;
        existingUser.LicenseKey = LicenseKey;
        existingUser.PremiumExpirationDate = PremiumExpirationDate;
        return existingUser;
    }
}

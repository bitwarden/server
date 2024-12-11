using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUpgradeRequestModel
{
    [StringLength(50)]
    public string BusinessName { get; set; }
    public PlanType PlanType { get; set; }

    [Range(0, int.MaxValue)]
    public int AdditionalSeats { get; set; }

    [Range(0, 99)]
    public short? AdditionalStorageGb { get; set; }

    [Range(0, int.MaxValue)]
    public int? AdditionalSmSeats { get; set; }

    [Range(0, int.MaxValue)]
    public int? AdditionalServiceAccounts { get; set; }

    [Required]
    public bool UseSecretsManager { get; set; }
    public bool PremiumAccessAddon { get; set; }
    public string BillingAddressCountry { get; set; }
    public string BillingAddressPostalCode { get; set; }
    public OrganizationKeysRequestModel Keys { get; set; }

    public OrganizationUpgrade ToOrganizationUpgrade()
    {
        var orgUpgrade = new OrganizationUpgrade
        {
            AdditionalSeats = AdditionalSeats,
            AdditionalStorageGb = AdditionalStorageGb.GetValueOrDefault(),
            AdditionalServiceAccounts = AdditionalServiceAccounts.GetValueOrDefault(0),
            AdditionalSmSeats = AdditionalSmSeats.GetValueOrDefault(0),
            UseSecretsManager = UseSecretsManager,
            BusinessName = BusinessName,
            Plan = PlanType,
            PremiumAccessAddon = PremiumAccessAddon,
            TaxInfo = new TaxInfo()
            {
                BillingAddressCountry = BillingAddressCountry,
                BillingAddressPostalCode = BillingAddressPostalCode,
            },
        };

        Keys?.ToOrganizationUpgrade(orgUpgrade);

        return orgUpgrade;
    }
}

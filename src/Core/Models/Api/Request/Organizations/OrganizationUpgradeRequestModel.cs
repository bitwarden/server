using Bit.Core.Enums;
using Bit.Core.Models.Business;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUpgradeRequestModel
    {
        [StringLength(50)]
        public string BusinessName { get; set; }
        public PlanType PlanType { get; set; }
        [Range(0, double.MaxValue)]
        public short AdditionalSeats { get; set; }
        [Range(0, 99)]
        public short? AdditionalStorageGb { get; set; }
        public bool PremiumAccessAddon { get; set; }
        public string BillingAddressCountry { get; set; }
        public string BillingAddressPostalCode { get; set; }

        public OrganizationUpgrade ToOrganizationUpgrade()
        {
            return new OrganizationUpgrade
            {
                AdditionalSeats = AdditionalSeats,
                AdditionalStorageGb = AdditionalStorageGb.GetValueOrDefault(),
                BusinessName = BusinessName,
                Plan = PlanType,
                PremiumAccessAddon = PremiumAccessAddon,
                TaxInfo = new TaxInfo()
                {
                    BillingAddressCountry = BillingAddressCountry,
                    BillingAddressPostalCode = BillingAddressPostalCode
                }
            };
        }
    }
}

using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Business
{
    public class OrganizationSignup
    {
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BillingEmail { get; set; }
        public User Owner { get; set; }
        public string OwnerKey { get; set; }
        public PlanType Plan { get; set; }
        public short AdditionalSeats { get; set; }
        public short AdditionalStorageGb { get; set; }
        public bool PremiumAccessAddon { get; set; }
        public PaymentMethodType? PaymentMethodType { get; set; }
        public string PaymentToken { get; set; }
        public string CollectionName { get; set; }
    }
}

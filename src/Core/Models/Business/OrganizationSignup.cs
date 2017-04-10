using Bit.Core.Models.Table;
using System;

namespace Bit.Core.Models.Business
{
    public class OrganizationSignup
    {
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BillingEmail { get; set; }
        public User Owner { get; set; }
        public string OwnerKey { get; set; }
        public Enums.PlanType Plan { get; set; }
        public short AdditionalSeats { get; set; }
        public string PaymentToken { get; set; }
    }
}

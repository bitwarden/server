using Bit.Core.Domains;
using System;

namespace Bit.Core.Models.Business
{
    public class OrganizationSignup
    {
        public string Name { get; set; }
        public User Owner { get; set; }
        public string OwnerKey { get; set; }
        public Enums.PlanType Plan { get; set; }
        public PaymentDetails Payment { get; set; }

        public class PaymentDetails
        {
            public string Name { get; set; }
            public string Token { get; set; }
        }
    }
}

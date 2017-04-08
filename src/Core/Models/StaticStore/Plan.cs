using Bit.Core.Enums;
using System;

namespace Bit.Core.Models.StaticStore
{
    public class Plan
    {
        public string Name { get; set; }
        public string StripeAnnualPlanId { get; set; }
        public string StripeAnnualUserPlanId { get; set; }
        public string StripeMonthlyPlanId { get; set; }
        public string StripeMonthlyUserPlanId { get; set; }
        public PlanType Type { get; set; }
        public short BaseUsers { get; set; }
        public bool CanBuyAdditionalUsers { get; set; }
        public short? MaxAdditionalUsers { get; set; }
        public bool CanMonthly { get; set; }
        public decimal BaseMonthlyPrice { get; set; }
        public decimal UserMonthlyPrice { get; set; }
        public decimal BaseAnnualPrice { get; set; }
        public decimal UserAnnualPrice { get; set; }
        public short? MaxSubvaults { get; set; }
        public bool Disabled { get; set; }
    }
}

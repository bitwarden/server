using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore
{
    public class Plan
    {
        public string Name { get; set; }
        public string StripePlanId { get; set; }
        public string StripeSeatPlanId { get; set; }
        public PlanType Type { get; set; }
        public short BaseSeats { get; set; }
        public bool CanBuyAdditionalSeats { get; set; }
        public short? MaxAdditionalSeats { get; set; }
        public bool UseGroups { get; set; }
        public decimal BasePrice { get; set; }
        public decimal SeatPrice { get; set; }
        public short? MaxCollections { get; set; }
        public int UpgradeSortOrder { get; set; }
        public bool Disabled { get; set; }
        public int? TrialPeriodDays { get; set; }
    }
}

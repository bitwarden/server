namespace Bit.Core.Models.Table
{
    public class PlanType: ITableObject<int>
    {
        public int Id { get; set; }
        public string StripePlanId { get; set; }
        public string StripeSeatPlanId { get; set; }
        public string StripeStoragePlanId { get; set; }
        public string StripePremiumAcessPlanId { get; set; }
        public double BasePrice { get; set; }
        public double SeatPrice { get; set; }
        public double AdditionalStoragePricePerGb { get; set; }
        public double PremiumAccessAddonCost { get; set; }
        public double IsAnnual { get; set; }
        public string PlanTypeGroupId { get; set; }

        public void SetNewId()
        {
            // do nothing because it is an identity
        }
    }
}

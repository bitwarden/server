using System;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class PlanTypeResponseModel : ResponseModel
    {
        public PlanTypeResponseModel(PlanTypePlanTypeGroup planTypePlanTypeGroup, string obj = "planType")
            : base(obj)
        {
            if (planTypePlanTypeGroup == null)
            {
                throw new ArgumentNullException(nameof(planTypePlanTypeGroup));
            }

            Id = planTypePlanTypeGroup.Id;
            StripePlanId = planTypePlanTypeGroup.StripePlanId;
            StripeSeatPlanId = planTypePlanTypeGroup.StripeSeatPlanId;
            StripePremiumAcessPlanId = planTypePlanTypeGroup.StripePremiumAcessPlanId;
            BasePrice = planTypePlanTypeGroup.BasePrice;
            SeatPrice = planTypePlanTypeGroup.SeatPrice;
            AdditionalStoragePricePerGb = planTypePlanTypeGroup.AdditionalStoragePricePerGb;
            PremiumAccessAddonCost = planTypePlanTypeGroup.PremiumAccessAddonCost;
            IsAnnual = planTypePlanTypeGroup.IsAnnual;
            PlanTypeGroupId = planTypePlanTypeGroup.PlanTypeGroupId;
            PlanTypeGroup = planTypePlanTypeGroup.PlanTypeGroup;
        }

        public int Id { get; set; }
        public string StripePlanId { get; set; }
        public string StripeSeatPlanId { get; set; }
        public string StripeStoragePlanId { get; set; }
        public string StripePremiumAcessPlanId { get; set; }
        public double BasePrice { get; set; }
        public double SeatPrice { get; set; }
        public double AdditionalStoragePricePerGb { get; set; }
        public double PremiumAccessAddonCost { get; set; }
        public bool IsAnnual { get; set; }
        public string PlanTypeGroupId { get; set; }
        public PlanTypeGroup PlanTypeGroup { get; set; }
    }
}

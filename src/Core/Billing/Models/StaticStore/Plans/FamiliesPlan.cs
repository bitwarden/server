using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record FamiliesPlan : Plan
{
    public FamiliesPlan()
    {
        Type = PlanType.FamiliesAnnually;
        ProductTier = ProductTierType.Families;
        Name = "Families";
        IsAnnual = true;
        NameLocalizationKey = "planNameFamilies";
        DescriptionLocalizationKey = "planDescFamilies";

        TrialPeriodDays = 7;

        HasSelfHost = true;
        HasTotp = true;
        UsersGetPremium = true;

        UpgradeSortOrder = 1;
        DisplaySortOrder = 1;

        PasswordManager = new TeamsPasswordManagerFeatures();
    }

    private record TeamsPasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public TeamsPasswordManagerFeatures()
        {
            BaseSeats = 6;
            BaseStorageGb = 1;
            MaxSeats = 6;

            HasAdditionalStorageOption = true;

            StripePlanId = "2020-families-org-annually";
            StripeStoragePlanId = "storage-gb-annually";
            BasePrice = 40;
            AdditionalStoragePricePerGb = 4;

            AllowSeatAutoscale = false;
        }
    }
}

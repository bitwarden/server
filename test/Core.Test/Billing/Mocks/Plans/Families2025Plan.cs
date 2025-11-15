using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Test.Billing.Mocks.Plans;

public record Families2025Plan : Plan
{
    public Families2025Plan()
    {
        Type = PlanType.FamiliesAnnually2025;
        ProductTier = ProductTierType.Families;
        Name = "Families 2025";
        IsAnnual = true;
        NameLocalizationKey = "planNameFamilies";
        DescriptionLocalizationKey = "planDescFamilies";

        TrialPeriodDays = 7;

        HasSelfHost = true;
        HasTotp = true;
        UsersGetPremium = true;

        UpgradeSortOrder = 1;
        DisplaySortOrder = 1;

        PasswordManager = new Families2025PasswordManagerFeatures();
    }

    private record Families2025PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Families2025PasswordManagerFeatures()
        {
            BaseSeats = 6;
            BaseStorageGb = 1;
            MaxSeats = 6;

            HasAdditionalStorageOption = true;

            StripePlanId = "2020-families-org-annually";
            StripeStoragePlanId = "personal-storage-gb-annually";
            BasePrice = 40;
            AdditionalStoragePricePerGb = 4;

            AllowSeatAutoscale = false;
        }
    }
}

using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record Families2019Plan : Plan
{
    public Families2019Plan()
    {
        Type = PlanType.FamiliesAnnually;
        Product = ProductType.Teams;
        Name = "Families 2019";
        IsAnnual = true;
        NameLocalizationKey = "planNameFamilies";
        DescriptionLocalizationKey = "planDescFamilies";
        TrialPeriodDays = 7;
        HasSelfHost = true;
        HasTotp = true;
        UsersGetPremium = true;
        UpgradeSortOrder = 1;
        DisplaySortOrder = 1;
        LegacyYear = 2020;
        PasswordManager = new Teams2019PasswordManagerFeatures();
    }

    private record Teams2019PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Teams2019PasswordManagerFeatures()
        {
            BaseSeats = 5;
            BaseStorageGb = 1;
            MaxUsers = 5;
            HasAdditionalStorageOption = true;
            StripePlanId = "personal-org-annually";
            StripeStoragePlanId = "storage-gb-annually";
            StripePremiumAccessPlanId = "personal-org-premium-access-annually";
            BasePrice = 12;
            AdditionalStoragePricePerGb = 4;
            PremiumAccessOptionPrice = 40;
        }
    }
}

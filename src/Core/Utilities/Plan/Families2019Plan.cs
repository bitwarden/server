using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record Families2019Plan : Models.StaticStore.Plan
{
    public Families2019Plan()
    {
        Type = PlanType.FamiliesAnnually2019;
        Product = ProductType.Families;
        Name = "Families 2019";
        IsAnnual = true;
        NameLocalizationKey = "planNameFamilies";
        DescriptionLocalizationKey = "planDescFamilies";
        TrialPeriodDays = 7;
        HasSelfHost = true;
        HasTotp = true;
        UpgradeSortOrder = 1;
        DisplaySortOrder = 1;
        LegacyYear = 2020;
        SupportsSecretsManager = false;
        PasswordManager = new Families2019PasswordManagerFeatures();
    }

    private record Families2019PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Families2019PasswordManagerFeatures()
        {
            BaseSeats = 5;
            BaseStorageGb = 1;
            MaxSeats = 5;
            AllowSeatAutoscale = false;
            HasAdditionalStorageOption = true;
            HasPremiumAccessOption = true;
            StripePlanId = "personal-org-annually";
            StripeStoragePlanId = "storage-gb-annually";
            StripePremiumAccessPlanId = "personal-org-premium-access-annually";
            BasePrice = 12;
            AdditionalStoragePricePerGb = 4;
            PremiumAccessOptionPrice = 40;
        }
    }
}

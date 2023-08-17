using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record FamiliesPlan : Models.StaticStore.Plan
{
    public FamiliesPlan()
    {
        Type = PlanType.FamiliesAnnually;
        Product = ProductType.Families;
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

        SupportsSecretsManager = false;

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

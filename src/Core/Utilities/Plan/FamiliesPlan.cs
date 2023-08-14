using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record FamiliesPlan : Plan
{
    public FamiliesPlan()
    {
        Type = PlanType.FamiliesAnnually;
        Product = ProductType.Teams;
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
             MaxUsers = 6;
             HasAdditionalStorageOption = true;
             StripePlanId = "2020-families-org-annually";
             StripeStoragePlanId = "storage-gb-annually";
             BasePrice = 40;
             AdditionalStoragePricePerGb = 4;
         }
     }
}

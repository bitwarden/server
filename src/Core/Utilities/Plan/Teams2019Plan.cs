using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record Teams2019Plan: Plan
{
    public Teams2019Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.TeamsAnnually2019 : PlanType.TeamsMonthly2019;
        Product = ProductType.Teams;
        Name = isAnnual ? "Teams (Annually) 2019" : "Teams (Monthly) 2019";
        IsAnnual = isAnnual;
        NameLocalizationKey = "planNameTeams";
        DescriptionLocalizationKey = "planDescTeams";
        CanBeUsedByBusiness = true;
        TrialPeriodDays = 7;
        HasTotp = true;
        UpgradeSortOrder = 2;
        DisplaySortOrder = 2;
        LegacyYear = 2020;
        PasswordManager = new Teams2019PasswordManagerFeatures(isAnnual);
    }

     private record Teams2019PasswordManagerFeatures : PasswordManagerPlanFeatures
     {
         public Teams2019PasswordManagerFeatures(bool isAnnual)
         {
             BaseSeats = 5;
             BaseStorageGb = 1;
             HasAdditionalStorageOption = true;
             HasAdditionalSeatsOption = true;
             AllowSeatAutoscale = true;

             if (isAnnual)
             {
                 StripePlanId = "teams-org-annually";
                 StripeStoragePlanId = "storage-gb-annually";
                 StripeSeatPlanId = "teams-org-seat-annually";
                 SeatPrice = 24;
                 BasePrice = 60;
                 AdditionalStoragePricePerGb = 4;
             }
             else
             {
                 StripePlanId = "teams-org-monthly";
                 StripeSeatPlanId = "teams-org-seat-monthly";
                 StripeStoragePlanId = "storage-gb-monthly";
                 BasePrice = 8;
                 SeatPrice = 2.5M;
                 AdditionalStoragePricePerGb = 0.5M;
             }
         }
     }
}

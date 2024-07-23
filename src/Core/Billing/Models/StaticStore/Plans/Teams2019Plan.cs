using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record Teams2019Plan : Plan
{
    public Teams2019Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.TeamsAnnually2019 : PlanType.TeamsMonthly2019;
        ProductTier = ProductTierType.Teams;
        Name = isAnnual ? "Teams (Annually) 2019" : "Teams (Monthly) 2019";
        IsAnnual = isAnnual;
        NameLocalizationKey = "planNameTeams";
        DescriptionLocalizationKey = "planDescTeams";
        CanBeUsedByBusiness = true;

        TrialPeriodDays = 7;

        HasGroups = true;
        HasDirectory = true;
        HasEvents = true;
        HasTotp = true;
        Has2fa = true;
        HasApi = true;
        UsersGetPremium = true;

        UpgradeSortOrder = 3;
        DisplaySortOrder = 3;
        LegacyYear = 2020;

        SecretsManager = new Teams2019SecretsManagerFeatures(isAnnual);
        PasswordManager = new Teams2019PasswordManagerFeatures(isAnnual);
    }

    private record Teams2019SecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public Teams2019SecretsManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BasePrice = 0;
            BaseServiceAccount = 50;

            HasAdditionalSeatsOption = true;
            HasAdditionalServiceAccountOption = true;

            AllowSeatAutoscale = true;
            AllowServiceAccountsAutoscale = true;

            if (isAnnual)
            {
                StripeSeatPlanId = "secrets-manager-teams-seat-annually";
                StripeServiceAccountPlanId = "secrets-manager-service-account-annually";
                SeatPrice = 72;
                AdditionalPricePerServiceAccount = 6;
            }
            else
            {
                StripeSeatPlanId = "secrets-manager-teams-seat-monthly";
                StripeServiceAccountPlanId = "secrets-manager-service-account-monthly";
                SeatPrice = 7;
                AdditionalPricePerServiceAccount = 0.5M;
            }
        }
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

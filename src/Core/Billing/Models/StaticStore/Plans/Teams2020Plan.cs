using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record Teams2020Plan : Plan
{
    public Teams2020Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.TeamsAnnually2020 : PlanType.TeamsMonthly2020;
        ProductTier = ProductTierType.Teams;
        Name = isAnnual ? "Teams (Annually) 2020" : "Teams (Monthly) 2020";
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
        LegacyYear = 2023;

        PasswordManager = new Teams2020PasswordManagerFeatures(isAnnual);
        SecretsManager = new Teams2020SecretsManagerFeatures(isAnnual);
    }

    private record Teams2020SecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public Teams2020SecretsManagerFeatures(bool isAnnual)
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

    private record Teams2020PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Teams2020PasswordManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BaseStorageGb = 1;
            BasePrice = 0;

            HasAdditionalStorageOption = true;
            HasAdditionalSeatsOption = true;

            AllowSeatAutoscale = true;

            if (isAnnual)
            {
                StripeStoragePlanId = "storage-gb-annually";
                StripeSeatPlanId = "2020-teams-org-seat-annually";
                SeatPrice = 36;
                AdditionalStoragePricePerGb = 4;
            }
            else
            {
                StripeSeatPlanId = "2020-teams-org-seat-monthly";
                StripeStoragePlanId = "storage-gb-monthly";
                SeatPrice = 4;
                AdditionalStoragePricePerGb = 0.5M;
            }
        }
    }
}

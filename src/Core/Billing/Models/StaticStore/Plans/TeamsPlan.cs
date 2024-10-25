using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record TeamsPlan : Plan
{
    public TeamsPlan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.TeamsAnnually : PlanType.TeamsMonthly;
        ProductTier = ProductTierType.Teams;
        Name = isAnnual ? "Teams (Annually)" : "Teams (Monthly)";
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
        HasScim = true;

        UpgradeSortOrder = 3;
        DisplaySortOrder = 3;

        PasswordManager = new TeamsPasswordManagerFeatures(isAnnual);
        SecretsManager = new TeamsSecretsManagerFeatures(isAnnual);
    }

    private record TeamsSecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public TeamsSecretsManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BasePrice = 0;
            BaseServiceAccount = 20;

            HasAdditionalSeatsOption = true;
            HasAdditionalServiceAccountOption = true;

            AllowSeatAutoscale = true;
            AllowServiceAccountsAutoscale = true;

            if (isAnnual)
            {
                StripeSeatPlanId = "secrets-manager-teams-seat-annually";
                StripeServiceAccountPlanId = "secrets-manager-service-account-2024-annually";
                SeatPrice = 72;
                AdditionalPricePerServiceAccount = 12;
            }
            else
            {
                StripeSeatPlanId = "secrets-manager-teams-seat-monthly";
                StripeServiceAccountPlanId = "secrets-manager-service-account-2024-monthly";
                SeatPrice = 7;
                AdditionalPricePerServiceAccount = 1;
            }
        }
    }

    private record TeamsPasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public TeamsPasswordManagerFeatures(bool isAnnual)
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
                StripeSeatPlanId = "2023-teams-org-seat-annually";
                SeatPrice = 48;
                AdditionalStoragePricePerGb = 4;
            }
            else
            {
                StripeSeatPlanId = "2023-teams-org-seat-monthly";
                StripeProviderPortalSeatPlanId = "password-manager-provider-portal-teams-monthly-2024";
                StripeStoragePlanId = "storage-gb-monthly";
                SeatPrice = 5;
                ProviderPortalSeatPrice = 4;
                AdditionalStoragePricePerGb = 0.5M;
            }
        }
    }
}

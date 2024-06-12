using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record TeamsStarterPlan2023 : Plan
{
    public TeamsStarterPlan2023()
    {
        Type = PlanType.TeamsStarter2023;
        ProductTier = ProductTierType.TeamsStarter;
        Name = "Teams (Starter)";
        NameLocalizationKey = "planNameTeamsStarter";
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

        UpgradeSortOrder = 2;
        DisplaySortOrder = 2;

        PasswordManager = new TeamsStarter2023PasswordManagerFeatures();
        SecretsManager = new TeamsStarter2023SecretsManagerFeatures();
        LegacyYear = 2024;
    }

    private record TeamsStarter2023SecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public TeamsStarter2023SecretsManagerFeatures()
        {
            BaseSeats = 0;
            BasePrice = 0;
            BaseServiceAccount = 50;

            HasAdditionalSeatsOption = true;
            HasAdditionalServiceAccountOption = true;

            AllowSeatAutoscale = true;
            AllowServiceAccountsAutoscale = true;

            StripeSeatPlanId = "secrets-manager-teams-seat-monthly";
            StripeServiceAccountPlanId = "secrets-manager-service-account-monthly";
            SeatPrice = 7;
            AdditionalPricePerServiceAccount = 0.5M;
        }
    }

    private record TeamsStarter2023PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public TeamsStarter2023PasswordManagerFeatures()
        {
            BaseSeats = 10;
            BaseStorageGb = 1;
            BasePrice = 20;

            MaxSeats = 10;

            HasAdditionalStorageOption = true;

            StripePlanId = "teams-org-starter";
            StripeStoragePlanId = "storage-gb-monthly";
            AdditionalStoragePricePerGb = 0.5M;
        }
    }
}

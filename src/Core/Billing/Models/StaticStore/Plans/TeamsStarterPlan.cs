using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record TeamsStarterPlan : Plan
{
    public TeamsStarterPlan()
    {
        Type = PlanType.TeamsStarter;
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

        PasswordManager = new TeamsStarterPasswordManagerFeatures();
        SecretsManager = new TeamsStarterSecretsManagerFeatures();

        LegacyYear = 2024;
    }

    private record TeamsStarterSecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public TeamsStarterSecretsManagerFeatures()
        {
            BaseSeats = 0;
            BasePrice = 0;
            BaseServiceAccount = 20;

            HasAdditionalSeatsOption = true;
            HasAdditionalServiceAccountOption = true;

            AllowSeatAutoscale = true;
            AllowServiceAccountsAutoscale = true;

            StripeSeatPlanId = "secrets-manager-teams-seat-monthly";
            StripeServiceAccountPlanId = "secrets-manager-service-account-2024-monthly";
            SeatPrice = 7;
            AdditionalPricePerServiceAccount = 1;
        }
    }

    private record TeamsStarterPasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public TeamsStarterPasswordManagerFeatures()
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

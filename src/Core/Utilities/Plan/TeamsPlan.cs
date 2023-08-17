using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record TeamsPlan : Models.StaticStore.Plan
{
    public TeamsPlan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.TeamsAnnually : PlanType.TeamsMonthly;
        Product = ProductType.Teams;
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
        UpgradeSortOrder = 2;
        DisplaySortOrder = 2;
        SupportsSecretsManager = true;
        PasswordManager = new TeamsPasswordManagerFeatures(isAnnual);
        SecretsManager = new TeamsSecretsManagerFeatures(isAnnual);
    }

    private record TeamsSecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public TeamsSecretsManagerFeatures(bool isAnnual)
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

    private record TeamsPasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public TeamsPasswordManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BaseStorageGb = 1;
            HasAdditionalStorageOption = true;
            HasAdditionalSeatsOption = true;
            BasePrice = 0;
            AllowSeatAutoscale = true;

            if (isAnnual)
            {
                AdditionalStoragePricePerGb = 4;
                StripeStoragePlanId = "storage-gb-annually";
                StripeSeatPlanId = "2020-teams-org-seat-annually";
                SeatPrice = 36;
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

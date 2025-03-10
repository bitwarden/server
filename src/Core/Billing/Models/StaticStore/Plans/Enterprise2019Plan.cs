using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record Enterprise2019Plan : Plan
{
    public Enterprise2019Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually2019 : PlanType.EnterpriseMonthly2019;
        ProductTier = ProductTierType.Enterprise;
        Name = isAnnual ? "Enterprise (Annually) 2019" : "Enterprise (Monthly) 2019";
        IsAnnual = isAnnual;
        NameLocalizationKey = "planNameEnterprise";
        DescriptionLocalizationKey = "planDescEnterprise";
        CanBeUsedByBusiness = true;

        TrialPeriodDays = 7;

        HasPolicies = true;
        HasSelfHost = true;
        HasGroups = true;
        HasDirectory = true;
        HasEvents = true;
        HasTotp = true;
        Has2fa = true;
        HasApi = true;
        HasSso = true;
        HasKeyConnector = true;
        HasScim = true;
        HasResetPassword = true;
        UsersGetPremium = true;
        HasCustomPermissions = true;

        UpgradeSortOrder = 4;
        DisplaySortOrder = 4;
        LegacyYear = 2020;

        SecretsManager = new Enterprise2019SecretsManagerFeatures(isAnnual);
        PasswordManager = new Enterprise2019PasswordManagerFeatures(isAnnual);
    }

    private record Enterprise2019SecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public Enterprise2019SecretsManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BasePrice = 0;
            BaseServiceAccount = 200;

            HasAdditionalSeatsOption = true;
            HasAdditionalServiceAccountOption = true;

            AllowSeatAutoscale = true;
            AllowServiceAccountsAutoscale = true;

            if (isAnnual)
            {
                StripeSeatPlanId = "secrets-manager-enterprise-seat-annually";
                StripeServiceAccountPlanId = "secrets-manager-service-account-annually";
                SeatPrice = 144;
                AdditionalPricePerServiceAccount = 6;
            }
            else
            {
                StripeSeatPlanId = "secrets-manager-enterprise-seat-monthly";
                StripeServiceAccountPlanId = "secrets-manager-service-account-monthly";
                SeatPrice = 13;
                AdditionalPricePerServiceAccount = 0.5M;
            }
        }
    }

    private record Enterprise2019PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Enterprise2019PasswordManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BaseStorageGb = 1;

            HasAdditionalStorageOption = true;
            HasAdditionalSeatsOption = true;

            AllowSeatAutoscale = true;

            if (isAnnual)
            {
                StripeStoragePlanId = "storage-gb-annually";
                StripeSeatPlanId = "enterprise-org-seat-annually";
                SeatPrice = 36;
                AdditionalStoragePricePerGb = 4;
            }
            else
            {
                StripeSeatPlanId = "enterprise-org-seat-monthly";
                StripeStoragePlanId = "storage-gb-monthly";
                SeatPrice = 4M;
                AdditionalStoragePricePerGb = 0.5M;
            }
        }
    }
}

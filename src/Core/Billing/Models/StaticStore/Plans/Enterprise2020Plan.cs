using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record Enterprise2020Plan : Plan
{
    public Enterprise2020Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually2020 : PlanType.EnterpriseMonthly2020;
        ProductTier = ProductTierType.Enterprise;
        Name = isAnnual ? "Enterprise (Annually) 2020" : "Enterprise (Monthly) 2020";
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
        LegacyYear = 2023;

        PasswordManager = new Enterprise2020PasswordManagerFeatures(isAnnual);
        SecretsManager = new Enterprise2020SecretsManagerFeatures(isAnnual);
    }

    private record Enterprise2020SecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public Enterprise2020SecretsManagerFeatures(bool isAnnual)
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

    private record Enterprise2020PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Enterprise2020PasswordManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BaseStorageGb = 1;

            HasAdditionalStorageOption = true;
            HasAdditionalSeatsOption = true;

            AllowSeatAutoscale = true;

            if (isAnnual)
            {
                AdditionalStoragePricePerGb = 4;
                StripeStoragePlanId = "storage-gb-annually";
                StripeSeatPlanId = "2020-enterprise-org-seat-annually";
                SeatPrice = 60;
            }
            else
            {
                StripeSeatPlanId = "2020-enterprise-seat-monthly";
                StripeStoragePlanId = "storage-gb-monthly";
                SeatPrice = 6;
                AdditionalStoragePricePerGb = 0.5M;
            }
        }
    }
}

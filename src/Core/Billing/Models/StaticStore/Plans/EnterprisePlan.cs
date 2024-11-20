using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

public record EnterprisePlan : Plan
{
    public EnterprisePlan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually : PlanType.EnterpriseMonthly;
        ProductTier = ProductTierType.Enterprise;
        Name = isAnnual ? "Enterprise (Annually)" : "Enterprise (Monthly)";
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

        PasswordManager = new EnterprisePasswordManagerFeatures(isAnnual);
        SecretsManager = new EnterpriseSecretsManagerFeatures(isAnnual);
    }

    private record EnterpriseSecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public EnterpriseSecretsManagerFeatures(bool isAnnual)
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
                StripeSeatPlanId = "secrets-manager-enterprise-seat-annually";
                StripeServiceAccountPlanId = "secrets-manager-service-account-2024-annually";
                SeatPrice = 144;
                AdditionalPricePerServiceAccount = 12;
            }
            else
            {
                StripeSeatPlanId = "secrets-manager-enterprise-seat-monthly";
                StripeServiceAccountPlanId = "secrets-manager-service-account-2024-monthly";
                SeatPrice = 13;
                AdditionalPricePerServiceAccount = 1;
            }
        }
    }

    private record EnterprisePasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public EnterprisePasswordManagerFeatures(bool isAnnual)
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
                StripeSeatPlanId = "2023-enterprise-org-seat-annually";
                StripeProviderPortalSeatPlanId = "password-manager-provider-portal-enterprise-annually-2024";
                SeatPrice = 72;
                ProviderPortalSeatPrice = 72;
            }
            else
            {
                StripeSeatPlanId = "2023-enterprise-seat-monthly";
                StripeProviderPortalSeatPlanId = "password-manager-provider-portal-enterprise-monthly-2024";
                StripeStoragePlanId = "storage-gb-monthly";
                SeatPrice = 7;
                ProviderPortalSeatPrice = 6;
                AdditionalStoragePricePerGb = 0.5M;
            }
        }
    }
}

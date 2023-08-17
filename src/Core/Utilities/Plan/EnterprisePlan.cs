using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record EnterprisePlan : Models.StaticStore.Plan
{
    public EnterprisePlan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually : PlanType.EnterpriseMonthly;
        Product = ProductType.Enterprise;
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

        UpgradeSortOrder = 3;
        DisplaySortOrder = 3;

        SupportsSecretsManager = true;

        PasswordManager = new EnterprisePasswordManagerFeatures(isAnnual);
        SecretsManager = new EnterpriseSecretsManagerFeatures(isAnnual);
    }

    private record EnterpriseSecretsManagerFeatures : SecretsManagerPlanFeatures
    {
        public EnterpriseSecretsManagerFeatures(bool isAnnual)
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

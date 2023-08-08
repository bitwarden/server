using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Utilities;

public record EnterprisePlan : Plan
{
    public EnterprisePlan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually : PlanType.EnterpriseMonthly;
        Name = "Enterprise (Annually)";
        Product = ProductType.Enterprise;
        IsAnnual = true;
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

        if (isAnnual)
        {
            PasswordManager = new PasswordManagerPlanFeatures
            {
                BaseSeats = 0,
                BaseStorageGb = 1,
                MaxCollections = 2,
                HasAdditionalStorageOption = true,
                HasAdditionalSeatsOption = true,
                HasPremiumAccessOption = true,
                AdditionalStoragePricePerGb = 4,
                StripeStoragePlanId = "storage-gb-annually",
                StripeSeatPlanId = "2020-enterprise-org-seat-annually",
                BasePrice = 0,
                SeatPrice = 60,
                AllowSeatAutoscale = true,
            };

            SecretsManager = new SecretsManagerPlanFeatures
            {
                BaseSeats = 0,
                BaseServiceAccount = 200,
                HasAdditionalSeatsOption = true,
                HasAdditionalServiceAccountOption = true,
                StripeSeatPlanId = "secrets-manager-enterprise-seat-annually",
                StripeServiceAccountPlanId = "secrets-manager-service-account-annually",
                BasePrice = 0,
                SeatPrice = 144,
                AdditionalPricePerServiceAccount = 6,
                AllowSeatAutoscale = true,
                AllowServiceAccountsAutoscale = true
            };
        }
        else
        {
            PasswordManager = new PasswordManagerPlanFeatures
            {
                BaseSeats = 0,
                BaseStorageGb = 1,
                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                StripeSeatPlanId = "2020-enterprise-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 0,
                SeatPrice = 6,
                AdditionalStoragePricePerGb = 0.5M,
                AllowSeatAutoscale = true,
            };

            SecretsManager = new SecretsManagerPlanFeatures
            {
                BaseSeats = 0,
                BaseServiceAccount = 200,
                HasAdditionalSeatsOption = true,
                HasAdditionalServiceAccountOption = true,
                StripeSeatPlanId = "secrets-manager-enterprise-seat-monthly",
                StripeServiceAccountPlanId = "secrets-manager-service-account-monthly",
                BasePrice = 0,
                SeatPrice = 13,
                AdditionalPricePerServiceAccount = 0.5M,
                AllowSeatAutoscale = true,
                AllowServiceAccountsAutoscale = true
            };
        }
    }
}

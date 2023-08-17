using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

public record Enterprise2019Plan : Models.StaticStore.Plan
{
    public Enterprise2019Plan(bool isAnnual)
    {
        Type = isAnnual ? PlanType.EnterpriseAnnually2019 : PlanType.EnterpriseMonthly2019;
        Product = ProductType.Enterprise;
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
        UsersGetPremium = true;
        HasCustomPermissions = true;
        UpgradeSortOrder = 3;
        DisplaySortOrder = 3;
        LegacyYear = 2020;
        SupportsSecretsManager = false;
        PasswordManager = new Enterprise2019PasswordManagerFeatures(isAnnual);
    }

    private record Enterprise2019PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Enterprise2019PasswordManagerFeatures(bool isAnnual)
        {
            BaseSeats = 0;
            BaseStorageGb = 1;
            HasAdditionalStorageOption = true;
            HasAdditionalSeatsOption = true;
            BasePrice = 0;
            AllowSeatAutoscale = true;

            if (isAnnual)
            {
                HasPremiumAccessOption = true;
                AdditionalStoragePricePerGb = 4;
                StripeStoragePlanId = "storage-gb-annually";
                StripeSeatPlanId = "enterprise-org-seat-annually";
                SeatPrice = 36;
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

using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Utilities;

public static class PasswordManagerPlanStore
{
    public static IEnumerable<Plan> CreatePlan()
    {
        return new List<Plan>
        {
            new Plan
            {
                Type = PlanType.Free,
                Product = ProductType.Free,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Free",
                NameLocalizationKey = "planNameFree",
                DescriptionLocalizationKey = "planDescFree",
                BaseSeats = 2,
                MaxCollections = 2,
                MaxUsers = 2,

                UpgradeSortOrder = -1, // Always the lowest plan, cannot be upgraded to
                DisplaySortOrder = -1,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.FamiliesAnnually2019,
                Product = ProductType.Families,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Families 2019",
                IsAnnual = true,
                NameLocalizationKey = "planNameFamilies",
                DescriptionLocalizationKey = "planDescFamilies",
                BaseSeats = 5,
                BaseStorageGb = 1,
                MaxUsers = 5,

                HasAdditionalStorageOption = true,
                HasPremiumAccessOption = true,
                TrialPeriodDays = 7,

                HasSelfHost = true,
                HasTotp = true,

                UpgradeSortOrder = 1,
                DisplaySortOrder = 1,
                LegacyYear = 2020,

                StripePlanId = "personal-org-annually",
                StripeStoragePlanId = "storage-gb-annually",
                StripePremiumAccessPlanId = "personal-org-premium-access-annually",
                BasePrice = 12,
                AdditionalStoragePricePerGb = 4,
                PremiumAccessOptionPrice = 40,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.TeamsAnnually2019,
                Product = ProductType.Teams,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Teams (Annually) 2019",
                IsAnnual = true,
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseSeats = 5,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasTotp = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,
                LegacyYear = 2020,

                StripePlanId = "teams-org-annually",
                StripeSeatPlanId = "teams-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 60,
                SeatPrice = 24,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.TeamsMonthly2019,
                Product = ProductType.Teams,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Teams (Monthly) 2019",
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseSeats = 5,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasTotp = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,
                LegacyYear = 2020,

                StripePlanId = "teams-org-monthly",
                StripeSeatPlanId = "teams-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 8,
                SeatPrice = 2.5M,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseAnnually2019,
                Name = "Enterprise (Annually) 2019",
                IsAnnual = true,
                Product = ProductType.Enterprise,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasSelfHost = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                UsersGetPremium = true,
                HasCustomPermissions = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,
                LegacyYear = 2020,

                StripePlanId = null,
                StripeSeatPlanId = "enterprise-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 0,
                SeatPrice = 36,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseMonthly2019,
                Product = ProductType.Enterprise,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Enterprise (Monthly) 2019",
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSelfHost = true,
                UsersGetPremium = true,
                HasCustomPermissions = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,
                LegacyYear = 2020,

                StripePlanId = null,
                StripeSeatPlanId = "enterprise-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 0,
                SeatPrice = 4M,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.FamiliesAnnually,
                Product = ProductType.Families,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Families",
                IsAnnual = true,
                NameLocalizationKey = "planNameFamilies",
                DescriptionLocalizationKey = "planDescFamilies",
                BaseSeats = 6,
                BaseStorageGb = 1,
                MaxUsers = 6,

                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasSelfHost = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 1,
                DisplaySortOrder = 1,

                StripePlanId = "2020-families-org-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 40,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.TeamsAnnually,
                Product = ProductType.Teams,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Teams (Annually)",
                IsAnnual = true,
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseStorageGb = 1,
                BaseSeats = 0,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                Has2fa = true,
                HasApi = true,
                HasDirectory = true,
                HasEvents = true,
                HasGroups = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,

                StripeSeatPlanId = "2020-teams-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                SeatPrice = 36,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.TeamsMonthly,
                Product = ProductType.Teams,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Teams (Monthly)",
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseStorageGb = 1,
                BaseSeats = 0,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                Has2fa = true,
                HasApi = true,
                HasDirectory = true,
                HasEvents = true,
                HasGroups = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,

                StripeSeatPlanId = "2020-teams-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                SeatPrice = 4,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                Product = ProductType.Enterprise,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                IsAnnual = true,
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasSelfHost = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSso = true,
                HasKeyConnector = true,
                HasScim = true,
                HasResetPassword = true,
                UsersGetPremium = true,
                HasCustomPermissions = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,

                StripeSeatPlanId = "2020-enterprise-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 0,
                SeatPrice = 60,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseMonthly,
                Product = ProductType.Enterprise,
                BitwardenProduct = BitwardenProductType.PasswordManager,
                Name = "Enterprise (Monthly)",
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSelfHost = true,
                HasSso = true,
                HasKeyConnector = true,
                HasScim = true,
                HasResetPassword = true,
                UsersGetPremium = true,
                HasCustomPermissions = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,

                StripeSeatPlanId = "2020-enterprise-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 0,
                SeatPrice = 6,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.Custom,

                AllowSeatAutoscale = true,
            },
        };
    }
}

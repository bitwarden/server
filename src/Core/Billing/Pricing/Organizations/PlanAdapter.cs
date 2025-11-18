using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Pricing.Organizations;

public record PlanAdapter : Core.Models.StaticStore.Plan
{
    public PlanAdapter(Plan plan)
    {
        Type = ToPlanType(plan.LookupKey);
        ProductTier = ToProductTierType(Type);
        Name = plan.Name;
        IsAnnual = plan.Cadence is "annually";
        NameLocalizationKey = plan.AdditionalData["nameLocalizationKey"];
        DescriptionLocalizationKey = plan.AdditionalData["descriptionLocalizationKey"];
        TrialPeriodDays = plan.TrialPeriodDays;
        HasSelfHost = HasFeature("selfHost");
        HasPolicies = HasFeature("policies");
        HasGroups = HasFeature("groups");
        HasDirectory = HasFeature("directory");
        HasEvents = HasFeature("events");
        HasTotp = HasFeature("totp");
        Has2fa = HasFeature("2fa");
        HasApi = HasFeature("api");
        HasSso = HasFeature("sso");
        HasOrganizationDomains = HasFeature("organizationDomains");
        HasKeyConnector = HasFeature("keyConnector");
        HasScim = HasFeature("scim");
        HasResetPassword = HasFeature("resetPassword");
        UsersGetPremium = HasFeature("usersGetPremium");
        HasCustomPermissions = HasFeature("customPermissions");
        UpgradeSortOrder = plan.AdditionalData.TryGetValue("upgradeSortOrder", out var upgradeSortOrder)
            ? int.Parse(upgradeSortOrder)
            : 0;
        DisplaySortOrder = plan.AdditionalData.TryGetValue("displaySortOrder", out var displaySortOrder)
            ? int.Parse(displaySortOrder)
            : 0;
        Disabled = !plan.Available;
        LegacyYear = plan.LegacyYear;
        PasswordManager = ToPasswordManagerPlanFeatures(plan);
        SecretsManager = plan.SecretsManager != null ? ToSecretsManagerPlanFeatures(plan) : null;

        return;

        bool HasFeature(string lookupKey) => plan.Features.Any(feature => feature.LookupKey == lookupKey);
    }

    #region Mappings

    private static PlanType ToPlanType(string lookupKey)
        => lookupKey switch
        {
            "enterprise-annually" => PlanType.EnterpriseAnnually,
            "enterprise-annually-2019" => PlanType.EnterpriseAnnually2019,
            "enterprise-annually-2020" => PlanType.EnterpriseAnnually2020,
            "enterprise-annually-2023" => PlanType.EnterpriseAnnually2023,
            "enterprise-monthly" => PlanType.EnterpriseMonthly,
            "enterprise-monthly-2019" => PlanType.EnterpriseMonthly2019,
            "enterprise-monthly-2020" => PlanType.EnterpriseMonthly2020,
            "enterprise-monthly-2023" => PlanType.EnterpriseMonthly2023,
            "families" => PlanType.FamiliesAnnually,
            "families-2025" => PlanType.FamiliesAnnually2025,
            "families-2019" => PlanType.FamiliesAnnually2019,
            "free" => PlanType.Free,
            "teams-annually" => PlanType.TeamsAnnually,
            "teams-annually-2019" => PlanType.TeamsAnnually2019,
            "teams-annually-2020" => PlanType.TeamsAnnually2020,
            "teams-annually-2023" => PlanType.TeamsAnnually2023,
            "teams-monthly" => PlanType.TeamsMonthly,
            "teams-monthly-2019" => PlanType.TeamsMonthly2019,
            "teams-monthly-2020" => PlanType.TeamsMonthly2020,
            "teams-monthly-2023" => PlanType.TeamsMonthly2023,
            "teams-starter" => PlanType.TeamsStarter,
            "teams-starter-2023" => PlanType.TeamsStarter2023,
            _ => throw new BillingException() // TODO: Flesh out
        };

    private static ProductTierType ToProductTierType(PlanType planType)
        => planType switch
        {
            PlanType.Free => ProductTierType.Free,
            PlanType.FamiliesAnnually or PlanType.FamiliesAnnually2025 or PlanType.FamiliesAnnually2019 => ProductTierType.Families,
            PlanType.TeamsStarter or PlanType.TeamsStarter2023 => ProductTierType.TeamsStarter,
            _ when planType.ToString().Contains("Teams") => ProductTierType.Teams,
            _ when planType.ToString().Contains("Enterprise") => ProductTierType.Enterprise,
            _ => throw new BillingException() // TODO: Flesh out
        };

    private static PasswordManagerPlanFeatures ToPasswordManagerPlanFeatures(Plan plan)
    {
        var stripePlanId = GetStripePlanId(plan.Seats);
        var stripeSeatPlanId = GetStripeSeatPlanId(plan.Seats);
        var stripeProviderPortalSeatPlanId = plan.ManagedSeats?.StripePriceId;
        var basePrice = GetBasePrice(plan.Seats);
        var seatPrice = GetSeatPrice(plan.Seats);
        var providerPortalSeatPrice = plan.ManagedSeats?.Price ?? 0;
        var scales = plan.Seats.Match(
            _ => false,
            packaged => packaged.Additional != null,
            _ => true);
        var baseSeats = GetBaseSeats(plan.Seats);
        var maxSeats = GetMaxSeats(plan.Seats);
        var baseStorageGb = (short)(plan.Storage?.Provided ?? 0);
        var hasAdditionalStorageOption = plan.Storage != null;
        var additionalStoragePricePerGb = plan.Storage?.Price ?? 0;
        var stripeStoragePlanId = plan.Storage?.StripePriceId;
        short? maxCollections = plan.AdditionalData.TryGetValue("passwordManager.maxCollections", out var value) ? short.Parse(value) : null;
        var stripePremiumAccessPlanId =
            plan.AdditionalData.TryGetValue("premiumAccessAddOnPriceId", out var premiumAccessAddOnPriceIdValue)
                ? premiumAccessAddOnPriceIdValue
                : null;
        var premiumAccessOptionPrice =
            plan.AdditionalData.TryGetValue("premiumAccessAddOnPriceAmount", out var premiumAccessAddOnPriceAmountValue)
                ? decimal.Parse(premiumAccessAddOnPriceAmountValue)
                : 0;

        return new PasswordManagerPlanFeatures
        {
            StripePlanId = stripePlanId,
            StripeSeatPlanId = stripeSeatPlanId,
            StripeProviderPortalSeatPlanId = stripeProviderPortalSeatPlanId,
            BasePrice = basePrice,
            SeatPrice = seatPrice,
            ProviderPortalSeatPrice = providerPortalSeatPrice,
            AllowSeatAutoscale = scales,
            HasAdditionalSeatsOption = scales,
            BaseSeats = baseSeats,
            MaxSeats = maxSeats,
            BaseStorageGb = baseStorageGb,
            HasAdditionalStorageOption = hasAdditionalStorageOption,
            AdditionalStoragePricePerGb = additionalStoragePricePerGb,
            StripeStoragePlanId = stripeStoragePlanId,
            MaxCollections = maxCollections,
            StripePremiumAccessPlanId = stripePremiumAccessPlanId,
            PremiumAccessOptionPrice = premiumAccessOptionPrice
        };
    }

    private static SecretsManagerPlanFeatures ToSecretsManagerPlanFeatures(Plan plan)
    {
        var seats = plan.SecretsManager!.Seats;
        var serviceAccounts = plan.SecretsManager.ServiceAccounts;

        var maxServiceAccounts = GetMaxServiceAccounts(serviceAccounts);
        var allowServiceAccountsAutoscale = serviceAccounts.IsScalable;
        var stripeServiceAccountPlanId = GetStripeServiceAccountPlanId(serviceAccounts);
        var additionalPricePerServiceAccount = GetAdditionalPricePerServiceAccount(serviceAccounts);
        var baseServiceAccount = GetBaseServiceAccount(serviceAccounts);
        var hasAdditionalServiceAccountOption = serviceAccounts.IsScalable;
        var stripeSeatPlanId = GetStripeSeatPlanId(seats);
        var hasAdditionalSeatsOption = seats.IsScalable;
        var seatPrice = GetSeatPrice(seats);
        var baseSeats = GetBaseSeats(seats);
        var maxSeats = GetMaxSeats(seats);
        var allowSeatAutoscale = seats.IsScalable;
        var maxProjects = plan.AdditionalData.TryGetValue("secretsManager.maxProjects", out var value) ? short.Parse(value) : 0;

        return new SecretsManagerPlanFeatures
        {
            MaxServiceAccounts = maxServiceAccounts,
            AllowServiceAccountsAutoscale = allowServiceAccountsAutoscale,
            StripeServiceAccountPlanId = stripeServiceAccountPlanId,
            AdditionalPricePerServiceAccount = additionalPricePerServiceAccount,
            BaseServiceAccount = baseServiceAccount,
            HasAdditionalServiceAccountOption = hasAdditionalServiceAccountOption,
            StripeSeatPlanId = stripeSeatPlanId,
            HasAdditionalSeatsOption = hasAdditionalSeatsOption,
            SeatPrice = seatPrice,
            BaseSeats = baseSeats,
            MaxSeats = maxSeats,
            AllowSeatAutoscale = allowSeatAutoscale,
            MaxProjects = maxProjects
        };
    }

    private static decimal? GetAdditionalPricePerServiceAccount(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromScalable(x => x.Price);

    private static decimal GetBasePrice(Purchasable purchasable)
        => purchasable.FromPackaged(x => x.Price);

    private static int GetBaseSeats(FreeOrScalable freeOrScalable)
        => freeOrScalable.Match(
            free => free.Quantity,
            scalable => scalable.Provided);

    private static int GetBaseSeats(Purchasable purchasable)
        => purchasable.Match(
            free => free.Quantity,
            packaged => packaged.Quantity,
            scalable => scalable.Provided);

    private static short GetBaseServiceAccount(FreeOrScalable freeOrScalable)
        => freeOrScalable.Match(
            free => (short)free.Quantity,
            scalable => (short)scalable.Provided);

    private static short? GetMaxSeats(Purchasable purchasable)
        => purchasable.Match<short?>(
            free => (short)free.Quantity,
            packaged => (short)packaged.Quantity,
            _ => null);

    private static short? GetMaxSeats(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromFree(x => (short)x.Quantity);

    private static short? GetMaxServiceAccounts(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromFree(x => (short)x.Quantity);

    private static decimal GetSeatPrice(Purchasable purchasable)
        => purchasable.Match(
            _ => 0,
            packaged => packaged.Additional?.Price ?? 0,
            scalable => scalable.Price);

    private static decimal GetSeatPrice(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromScalable(x => x.Price);

    private static string? GetStripePlanId(Purchasable purchasable)
        => purchasable.FromPackaged(x => x.StripePriceId);

    private static string? GetStripeSeatPlanId(Purchasable purchasable)
        => purchasable.Match(
            _ => null,
            packaged => packaged.Additional?.StripePriceId,
            scalable => scalable.StripePriceId);

    private static string? GetStripeSeatPlanId(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromScalable(x => x.StripePriceId);

    private static string? GetStripeServiceAccountPlanId(FreeOrScalable freeOrScalable)
        => freeOrScalable.FromScalable(x => x.StripePriceId);

    #endregion
}

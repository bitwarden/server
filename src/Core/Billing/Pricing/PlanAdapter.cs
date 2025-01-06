using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;
using Proto.Billing.Pricing;

#nullable enable

namespace Bit.Core.Billing.Pricing;

public record PlanAdapter : Plan
{
    public PlanAdapter(PlanResponse planResponse)
    {
        Type = ToPlanType(planResponse.LookupKey);
        ProductTier = ToProductTierType(Type);
        Name = planResponse.Name;
        IsAnnual = !string.IsNullOrEmpty(planResponse.Cadence) && planResponse.Cadence == "annually";
        NameLocalizationKey = planResponse.AdditionalData?["nameLocalizationKey"];
        DescriptionLocalizationKey = planResponse.AdditionalData?["descriptionLocalizationKey"];
        TrialPeriodDays = planResponse.TrialPeriodDays;
        HasSelfHost = HasFeature("selfHost");
        HasPolicies = HasFeature("policies");
        HasGroups = HasFeature("groups");
        HasDirectory = HasFeature("directory");
        HasEvents = HasFeature("events");
        HasTotp = HasFeature("totp");
        Has2fa = HasFeature("2fa");
        HasApi = HasFeature("api");
        HasSso = HasFeature("sso");
        HasKeyConnector = HasFeature("keyConnector");
        HasScim = HasFeature("scim");
        HasResetPassword = HasFeature("resetPassword");
        UsersGetPremium = HasFeature("usersGetPremium");
        UpgradeSortOrder = planResponse.AdditionalData != null
            ? int.Parse(planResponse.AdditionalData["upgradeSortOrder"])
            : 0;
        DisplaySortOrder = planResponse.AdditionalData != null
            ? int.Parse(planResponse.AdditionalData["displaySortOrder"])
            : 0;
        HasCustomPermissions = HasFeature("customPermissions");
        Disabled = !planResponse.Available;
        PasswordManager = ToPasswordManagerPlanFeatures(planResponse);
        SecretsManager = planResponse.SecretsManager != null ? ToSecretsManagerPlanFeatures(planResponse) : null;

        return;

        bool HasFeature(string lookupKey) => planResponse.Features.Any(feature => feature.LookupKey == lookupKey);
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
            PlanType.FamiliesAnnually or PlanType.FamiliesAnnually2019 => ProductTierType.Families,
            PlanType.TeamsStarter or PlanType.TeamsStarter2023 => ProductTierType.TeamsStarter,
            _ when planType.ToString().Contains("Teams") => ProductTierType.Teams,
            _ when planType.ToString().Contains("Enterprise") => ProductTierType.Enterprise,
            _ => throw new BillingException() // TODO: Flesh out
        };

    private static PasswordManagerPlanFeatures ToPasswordManagerPlanFeatures(PlanResponse planResponse)
    {
        var stripePlanId = GetStripePlanId(planResponse.Seats);
        var stripeSeatPlanId = GetStripeSeatPlanId(planResponse.Seats);
        var stripeProviderPortalSeatPlanId = planResponse.ManagedSeats?.StripePriceId;
        var basePrice = GetBasePrice(planResponse.Seats);
        var seatPrice = GetSeatPrice(planResponse.Seats);
        var providerPortalSeatPrice =
            planResponse.ManagedSeats != null ? decimal.Parse(planResponse.ManagedSeats.Price) : 0;
        var scales = planResponse.Seats.KindCase switch
        {
            PurchasableDTO.KindOneofCase.Scalable => true,
            PurchasableDTO.KindOneofCase.Packaged => planResponse.Seats.Packaged.Additional != null,
            _ => false
        };
        var baseSeats = GetBaseSeats(planResponse.Seats);
        var maxSeats = GetMaxSeats(planResponse.Seats);
        var baseStorageGb = (short?)planResponse.Storage?.Provided;
        var hasAdditionalStorageOption = planResponse.Storage != null;
        var stripeStoragePlanId = planResponse.Storage?.StripePriceId;
        short? maxCollections =
            planResponse.AdditionalData != null &&
            planResponse.AdditionalData.TryGetValue("passwordManager.maxCollections", out var value) ? short.Parse(value) : null;

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
            StripeStoragePlanId = stripeStoragePlanId,
            MaxCollections = maxCollections
        };
    }

    private static SecretsManagerPlanFeatures ToSecretsManagerPlanFeatures(PlanResponse planResponse)
    {
        var seats = planResponse.SecretsManager.Seats;
        var serviceAccounts = planResponse.SecretsManager.ServiceAccounts;

        var maxServiceAccounts = GetMaxServiceAccounts(serviceAccounts);
        var allowServiceAccountsAutoscale = serviceAccounts.KindCase == FreeOrScalableDTO.KindOneofCase.Scalable;
        var stripeServiceAccountPlanId = GetStripeServiceAccountPlanId(serviceAccounts);
        var additionalPricePerServiceAccount = GetAdditionalPricePerServiceAccount(serviceAccounts);
        var baseServiceAccount = GetBaseServiceAccount(serviceAccounts);
        var hasAdditionalServiceAccountOption = serviceAccounts.KindCase == FreeOrScalableDTO.KindOneofCase.Scalable;
        var stripeSeatPlanId = GetStripeSeatPlanId(seats);
        var hasAdditionalSeatsOption = seats.KindCase == FreeOrScalableDTO.KindOneofCase.Scalable;
        var seatPrice = GetSeatPrice(seats);
        var maxSeats = GetMaxSeats(seats);
        var allowSeatAutoscale = seats.KindCase == FreeOrScalableDTO.KindOneofCase.Scalable;
        var maxProjects =
            planResponse.AdditionalData != null &&
            planResponse.AdditionalData.TryGetValue("secretsManager.maxProjects", out var value) ? short.Parse(value) : 0;

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
            MaxSeats = maxSeats,
            AllowSeatAutoscale = allowSeatAutoscale,
            MaxProjects = maxProjects
        };
    }

    private static decimal? GetAdditionalPricePerServiceAccount(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Scalable
            ? null
            : decimal.Parse(freeOrScalable.Scalable.Price);

    private static decimal GetBasePrice(PurchasableDTO purchasable)
        => purchasable.KindCase != PurchasableDTO.KindOneofCase.Packaged ? 0 : decimal.Parse(purchasable.Packaged.Price);

    private static int GetBaseSeats(PurchasableDTO purchasable)
        => purchasable.KindCase != PurchasableDTO.KindOneofCase.Packaged ? 0 : purchasable.Packaged.Quantity;

    private static short GetBaseServiceAccount(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase switch
        {
            FreeOrScalableDTO.KindOneofCase.Free => (short)freeOrScalable.Free.Quantity,
            FreeOrScalableDTO.KindOneofCase.Scalable => (short)freeOrScalable.Scalable.Provided,
            _ => 0
        };

    private static short? GetMaxSeats(PurchasableDTO purchasable)
        => purchasable.KindCase != PurchasableDTO.KindOneofCase.Free ? null : (short)purchasable.Free.Quantity;

    private static short? GetMaxSeats(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Free ? null : (short)freeOrScalable.Free.Quantity;

    private static short? GetMaxServiceAccounts(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Free ? null : (short)freeOrScalable.Free.Quantity;

    private static decimal GetSeatPrice(PurchasableDTO purchasable)
        => purchasable.KindCase switch
        {
            PurchasableDTO.KindOneofCase.Packaged => purchasable.Packaged.Additional != null ? decimal.Parse(purchasable.Packaged.Additional.Price) : 0,
            PurchasableDTO.KindOneofCase.Scalable => decimal.Parse(purchasable.Scalable.Price),
            _ => 0
        };

    private static decimal GetSeatPrice(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Scalable
            ? 0
            : decimal.Parse(freeOrScalable.Scalable.Price);

    private static string? GetStripePlanId(PurchasableDTO purchasable)
        => purchasable.KindCase != PurchasableDTO.KindOneofCase.Packaged ? null : purchasable.Packaged.StripePriceId;

    private static string? GetStripeSeatPlanId(PurchasableDTO purchasable)
        => purchasable.KindCase switch
        {
            PurchasableDTO.KindOneofCase.Packaged => purchasable.Packaged.Additional?.StripePriceId,
            PurchasableDTO.KindOneofCase.Scalable => purchasable.Scalable.StripePriceId,
            _ => null
        };

    private static string? GetStripeSeatPlanId(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Scalable
            ? null
            : freeOrScalable.Scalable.StripePriceId;

    private static string? GetStripeServiceAccountPlanId(FreeOrScalableDTO freeOrScalable)
        => freeOrScalable.KindCase != FreeOrScalableDTO.KindOneofCase.Scalable
            ? null
            : freeOrScalable.Scalable.StripePriceId;

    #endregion
}

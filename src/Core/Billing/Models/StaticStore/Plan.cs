﻿using Bit.Core.Billing.Enums;

namespace Bit.Core.Models.StaticStore;

public abstract record Plan
{
    public PlanType Type { get; protected init; }
    public ProductTierType ProductTier { get; protected init; }
    public string Name { get; protected init; }
    public bool IsAnnual { get; protected init; }
    // TODO: Move to the client
    public string NameLocalizationKey { get; protected init; }
    // TODO: Move to the client
    public string DescriptionLocalizationKey { get; protected init; }
    // TODO: Remove
    public bool CanBeUsedByBusiness { get; protected init; }
    public int? TrialPeriodDays { get; protected init; }
    public bool HasSelfHost { get; protected init; }
    public bool HasPolicies { get; protected init; }
    public bool HasGroups { get; protected init; }
    public bool HasDirectory { get; protected init; }
    public bool HasEvents { get; protected init; }
    public bool HasTotp { get; protected init; }
    public bool Has2fa { get; protected init; }
    public bool HasApi { get; protected init; }
    public bool HasSso { get; protected init; }
    public bool HasKeyConnector { get; protected init; }
    public bool HasScim { get; protected init; }
    public bool HasResetPassword { get; protected init; }
    public bool UsersGetPremium { get; protected init; }
    public bool HasCustomPermissions { get; protected init; }
    public int UpgradeSortOrder { get; protected init; }
    // TODO: Move to the client
    public int DisplaySortOrder { get; protected init; }
    // TODO: Remove
    public int? LegacyYear { get; protected init; }
    public bool Disabled { get; protected init; }
    public PasswordManagerPlanFeatures PasswordManager { get; protected init; }
    public SecretsManagerPlanFeatures SecretsManager { get; protected init; }
    public bool SupportsSecretsManager => SecretsManager != null;

    public bool HasNonSeatBasedPasswordManagerPlan() =>
        PasswordManager is { StripePlanId: not null and not "", StripeSeatPlanId: null or "" };

    public record SecretsManagerPlanFeatures
    {
        // Service accounts
        public short? MaxServiceAccounts { get; init; }
        public bool AllowServiceAccountsAutoscale { get; init; }
        public string StripeServiceAccountPlanId { get; init; }
        public decimal? AdditionalPricePerServiceAccount { get; init; }
        public short BaseServiceAccount { get; init; }
        // TODO: Unused, remove
        public short? MaxAdditionalServiceAccount { get; init; }
        public bool HasAdditionalServiceAccountOption { get; init; }
        // Seats
        public string StripeSeatPlanId { get; init; }
        public bool HasAdditionalSeatsOption { get; init; }
        // TODO: Remove, SM is never packaged
        public decimal BasePrice { get; init; }
        public decimal SeatPrice { get; init; }
        // TODO: Remove, SM is never packaged
        public int BaseSeats { get; init; }
        public short? MaxSeats { get; init; }
        // TODO: Unused, remove
        public int? MaxAdditionalSeats { get; init; }
        public bool AllowSeatAutoscale { get; init; }

        // Features
        public int MaxProjects { get; init; }
    }

    public record PasswordManagerPlanFeatures
    {
        // Seats
        public string StripePlanId { get; init; }
        public string StripeSeatPlanId { get; init; }
        [Obsolete("No longer used to retrieve a provider's price ID. Use ProviderPriceAdapter instead.")]
        public string StripeProviderPortalSeatPlanId { get; init; }
        public decimal BasePrice { get; init; }
        public decimal SeatPrice { get; init; }
        public decimal ProviderPortalSeatPrice { get; init; }
        public bool AllowSeatAutoscale { get; init; }
        public bool HasAdditionalSeatsOption { get; init; }
        // TODO: Remove, never set.
        public int? MaxAdditionalSeats { get; init; }
        public int BaseSeats { get; init; }
        // TODO: Remove premium access as it's deprecated
        public bool HasPremiumAccessOption { get; init; }
        public string StripePremiumAccessPlanId { get; init; }
        public decimal PremiumAccessOptionPrice { get; init; }
        public short? MaxSeats { get; init; }
        // Storage
        public short? BaseStorageGb { get; init; }
        public bool HasAdditionalStorageOption { get; init; }
        public decimal AdditionalStoragePricePerGb { get; init; }
        public string StripeStoragePlanId { get; init; }
        // TODO: Remove
        public short? MaxAdditionalStorage { get; init; }
        // Feature
        public short? MaxCollections { get; init; }
    }
}

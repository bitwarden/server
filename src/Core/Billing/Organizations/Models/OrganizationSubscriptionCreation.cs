using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Organizations.Models;

#nullable enable

/// <summary>
/// Represents the data required to create a new subscription for an organization, including plan type, purchased products, customer setup, trial setup, and owner information.
/// </summary>
public record OrganizationSubscriptionCreation
{
    public required PlanType PlanType { get; init; }
    public required PasswordManagerPurchase PasswordManager { get; init; }
    public SecretsManagerPurchase? SecretsManager { get; init; }
    public PrivilegedControlsPurchase? PrivilegedControls { get; init; }
    public CustomerSetup? CustomerSetup { get; init; }
    public TrialSetup Trial { get; init; } = new();
    public User? Owner { get; init; }
    public required string CloudRegion { get; init; }

    public record PasswordManagerPurchase
    {
        public required int Seats { get; init; }
        public short? Storage { get; init; }
        public bool PremiumAccess { get; init; }
    }

    public record SecretsManagerPurchase
    {
        public required int Seats { get; init; }
        public int? ServiceAccounts { get; init; }
    }

    public record PrivilegedControlsPurchase
    {
        public int? RequestedSeatMinimum { get; init; }
    }

    public record TrialSetup
    {
        public int? Length { get; init; }
        public bool Skip { get; init; }
        public string? InitiationPath { get; init; }
    }
}

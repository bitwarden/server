using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Organizations.Models;

public record OrganizationSubscriptionPurchase
{
    public ProductTierType Tier { get; init; }
    public PlanCadenceType Cadence { get; init; }
    public required PasswordManagerSelections PasswordManager { get; init; }
    public SecretsManagerSelections? SecretsManager { get; init; }

    public PlanType PlanType =>
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        Tier switch
        {
            ProductTierType.Families => PlanType.FamiliesAnnually,
            ProductTierType.Teams => Cadence == PlanCadenceType.Monthly
                ? PlanType.TeamsMonthly
                : PlanType.TeamsAnnually,
            ProductTierType.Enterprise => Cadence == PlanCadenceType.Monthly
                ? PlanType.EnterpriseMonthly
                : PlanType.EnterpriseAnnually,
            _ => throw new InvalidOperationException("Cannot purchase an Organization subscription that isn't Families, Teams or Enterprise.")
        };

    public record PasswordManagerSelections
    {
        public int Seats { get; init; }
        public int AdditionalStorage { get; init; }
        public bool Sponsored { get; init; }
    }

    public record SecretsManagerSelections
    {
        public int Seats { get; init; }
        public int AdditionalServiceAccounts { get; init; }
        public bool Standalone { get; init; }
    }
}

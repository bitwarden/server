using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class BillingExtensions
{
    public static ProductTierType GetProductTier(this PlanType planType)
        => planType switch
        {
            PlanType.Custom or PlanType.Free => ProductTierType.Free,
            PlanType.FamiliesAnnually or PlanType.FamiliesAnnually2019 => ProductTierType.Families,
            PlanType.TeamsStarter or PlanType.TeamsStarter2023 => ProductTierType.TeamsStarter,
            _ when planType.ToString().Contains("Teams") => ProductTierType.Teams,
            _ when planType.ToString().Contains("Enterprise") => ProductTierType.Enterprise,
            _ => throw new BillingException($"PlanType {planType} could not be matched to a ProductTierType")
        };

    public static bool IsBusinessProductTierType(this PlanType planType)
        => IsBusinessProductTierType(planType.GetProductTier());

    public static bool IsBusinessProductTierType(this ProductTierType productTierType)
        => productTierType switch
        {
            ProductTierType.Free => false,
            ProductTierType.Families => false,
            ProductTierType.Enterprise => true,
            ProductTierType.Teams => true,
            ProductTierType.TeamsStarter => true
        };

    public static bool IsBillable(this Provider provider) =>
        provider is
        {
            Type: ProviderType.Msp or ProviderType.BusinessUnit,
            Status: ProviderStatusType.Billable
        };

    public static bool IsBillable(this InviteOrganizationProvider inviteOrganizationProvider) =>
        inviteOrganizationProvider is
        {
            Type: ProviderType.Msp or ProviderType.BusinessUnit,
            Status: ProviderStatusType.Billable
        };

    // Reseller types do not have Stripe entities
    public static bool IsStripeSupported(this ProviderType providerType) =>
        providerType is ProviderType.Msp or ProviderType.BusinessUnit;

    public static bool SupportsConsolidatedBilling(this ProviderType providerType)
        => providerType is ProviderType.Msp or ProviderType.BusinessUnit;

    public static bool IsValidClient(this Organization organization)
        => organization is
        {
            Seats: not null,
            Status: OrganizationStatusType.Managed,
            PlanType: PlanType.TeamsMonthly or PlanType.EnterpriseMonthly or PlanType.EnterpriseAnnually
        };

    public static bool IsStripeEnabled(this ISubscriber subscriber)
        => subscriber is
        {
            GatewayCustomerId: not null and not "",
            GatewaySubscriptionId: not null and not ""
        };

    public static bool IsUnverifiedBankAccount(this SetupIntent setupIntent) =>
        setupIntent is
        {
            Status: "requires_action",
            NextAction:
            {
                VerifyWithMicrodeposits: not null
            },
            PaymentMethod:
            {
                UsBankAccount: not null
            }
        };

    public static bool SupportsConsolidatedBilling(this PlanType planType)
        => planType is PlanType.TeamsMonthly or PlanType.EnterpriseMonthly or PlanType.EnterpriseAnnually;
}

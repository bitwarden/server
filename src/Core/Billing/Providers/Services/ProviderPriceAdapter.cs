// ReSharper disable SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Stripe;

namespace Bit.Core.Billing.Providers.Services;

public static class ProviderPriceAdapter
{
    public static class MSP
    {
        public static class Active
        {
            public const string Enterprise = "provider-portal-enterprise-monthly-2025";
            public const string Teams = "provider-portal-teams-monthly-2025";
        }

        public static class Legacy
        {
            public const string Enterprise = "password-manager-provider-portal-enterprise-monthly-2024";
            public const string Teams = "password-manager-provider-portal-teams-monthly-2024";
            public static readonly List<string> List = [Enterprise, Teams];
        }
    }

    public static class BusinessUnit
    {
        public static class Active
        {
            public const string Annually = "business-unit-portal-enterprise-annually-2025";
            public const string Monthly = "business-unit-portal-enterprise-monthly-2025";
        }

        public static class Legacy
        {
            public const string Annually = "password-manager-provider-portal-enterprise-annually-2024";
            public const string Monthly = "password-manager-provider-portal-enterprise-monthly-2024";
            public static readonly List<string> List = [Annually, Monthly];
        }
    }

    /// <summary>
    /// Uses the <paramref name="provider"/>'s <see cref="Provider.Type"/> and <paramref name="subscription"/> to determine
    /// whether the <paramref name="provider"/> is on active or legacy pricing and then returns a Stripe price ID for the provided
    /// <paramref name="planType"/> based on that determination.
    /// </summary>
    /// <param name="provider">The provider to get the Stripe price ID for.</param>
    /// <param name="subscription">The provider's subscription.</param>
    /// <param name="planType">The plan type correlating to the desired Stripe price ID.</param>
    /// <returns>A Stripe <see cref="Stripe.Price"/> ID.</returns>
    /// <exception cref="BillingException">Thrown when the provider's type is not <see cref="ProviderType.Msp"/> or <see cref="ProviderType.BusinessUnit"/>.</exception>
    /// <exception cref="BillingException">Thrown when the provided <paramref name="planType"/> does not relate to a Stripe price ID.</exception>
    public static string GetPriceId(
        Provider provider,
        Subscription subscription,
        PlanType planType)
    {
        var priceIds = subscription.Items.Select(item => item.Price.Id);

        var invalidPlanType =
            new BillingException(message: $"PlanType {planType} does not have an associated provider price in Stripe");

        return provider.Type switch
        {
            ProviderType.Msp => MSP.Legacy.List.Intersect(priceIds).Any()
                ? planType switch
                {
                    PlanType.TeamsMonthly => MSP.Legacy.Teams,
                    PlanType.EnterpriseMonthly => MSP.Legacy.Enterprise,
                    _ => throw invalidPlanType
                }
                : planType switch
                {
                    PlanType.TeamsMonthly => MSP.Active.Teams,
                    PlanType.EnterpriseMonthly => MSP.Active.Enterprise,
                    _ => throw invalidPlanType
                },
            ProviderType.BusinessUnit => BusinessUnit.Legacy.List.Intersect(priceIds).Any()
                ? planType switch
                {
                    PlanType.EnterpriseAnnually => BusinessUnit.Legacy.Annually,
                    PlanType.EnterpriseMonthly => BusinessUnit.Legacy.Monthly,
                    _ => throw invalidPlanType
                }
                : planType switch
                {
                    PlanType.EnterpriseAnnually => BusinessUnit.Active.Annually,
                    PlanType.EnterpriseMonthly => BusinessUnit.Active.Monthly,
                    _ => throw invalidPlanType
                },
            _ => throw new BillingException(
                $"ProviderType {provider.Type} does not have any associated provider price IDs")
        };
    }

    /// <summary>
    /// Uses the <paramref name="provider"/>'s <see cref="Provider.Type"/> to return the active Stripe price ID for the provided
    /// <paramref name="planType"/>.
    /// </summary>
    /// <param name="provider">The provider to get the Stripe price ID for.</param>
    /// <param name="planType">The plan type correlating to the desired Stripe price ID.</param>
    /// <returns>A Stripe <see cref="Stripe.Price"/> ID.</returns>
    /// <exception cref="BillingException">Thrown when the provider's type is not <see cref="ProviderType.Msp"/> or <see cref="ProviderType.BusinessUnit"/>.</exception>
    /// <exception cref="BillingException">Thrown when the provided <paramref name="planType"/> does not relate to a Stripe price ID.</exception>
    public static string GetActivePriceId(
        Provider provider,
        PlanType planType)
    {
        var invalidPlanType =
            new BillingException(message: $"PlanType {planType} does not have an associated provider price in Stripe");

        return provider.Type switch
        {
            ProviderType.Msp => planType switch
            {
                PlanType.TeamsMonthly => MSP.Active.Teams,
                PlanType.EnterpriseMonthly => MSP.Active.Enterprise,
                _ => throw invalidPlanType
            },
            ProviderType.BusinessUnit => planType switch
            {
                PlanType.EnterpriseAnnually => BusinessUnit.Active.Annually,
                PlanType.EnterpriseMonthly => BusinessUnit.Active.Monthly,
                _ => throw invalidPlanType
            },
            _ => throw new BillingException(
                $"ProviderType {provider.Type} does not have any associated provider price IDs")
        };
    }
}

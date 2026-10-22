using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Models.Business;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Helpers;

public class PrivilegedControlsTrialHelpersTests
{
    [Fact]
    public void IsOnSalesInitiatedTrial_SubscriptionInfoOverload_PamOnPlanSupportsAndNoSubscriptionInfo_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true));

        Assert.True(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_SubscriptionInfoOverload_PamOnAndSubscriptionHasNoPamLine_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true), SubscriptionInfoWithPamLine(false));

        Assert.True(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_SubscriptionInfoOverload_SubscriptionHasPamLine_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true), SubscriptionInfoWithPamLine(true));

        Assert.False(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_SubscriptionInfoOverload_PamOff_ReturnsFalse()
    {
        var organization = new Organization { UsePam = false };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true));

        Assert.False(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_SubscriptionInfoOverload_PlanDoesNotSupportPrivilegedControls_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new TeamsPlan(isAnnual: true));

        Assert.False(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_StripeSubscriptionOverload_NoSubscription_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true), (Subscription?)null);

        Assert.True(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_StripeSubscriptionOverload_SubscriptionHasNoPamLine_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true),
            StripeSubscriptionWithReference(StripeConstants.PurchasableReferences.PasswordManagerSeat));

        Assert.True(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_StripeSubscriptionOverload_SubscriptionHasPamLine_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true),
            StripeSubscriptionWithReference(StripeConstants.PurchasableReferences.PrivilegedControlsSeat));

        Assert.False(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_StripeSubscriptionOverload_PamOff_ReturnsFalse()
    {
        var organization = new Organization { UsePam = false };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new EnterprisePlan(isAnnual: true),
            StripeSubscriptionWithReference(StripeConstants.PurchasableReferences.PasswordManagerSeat));

        Assert.False(result);
    }

    [Fact]
    public void IsOnSalesInitiatedTrial_StripeSubscriptionOverload_PlanDoesNotSupportPrivilegedControls_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrialHelpers.IsOnSalesInitiatedTrial(
            organization, new TeamsPlan(isAnnual: true),
            StripeSubscriptionWithReference(StripeConstants.PurchasableReferences.PasswordManagerSeat));

        Assert.False(result);
    }

    private static SubscriptionInfo SubscriptionInfoWithPamLine(bool hasPamLine) => new()
    {
        Subscription = new SubscriptionInfo.BillingSubscription(null!) { HasPrivilegedControlsSeats = hasPamLine }
    };

    private static Subscription StripeSubscriptionWithReference(string reference) => new()
    {
        Items = new StripeList<SubscriptionItem>
        {
            Data =
            [
                new SubscriptionItem
                {
                    Price = new Price
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            [StripeConstants.MetadataKeys.PurchasableReference] = reference
                        }
                    }
                }
            ]
        }
    };
}

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Models.Business;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Helpers;

public class PrivilegedControlsTrialTests
{
    [Fact]
    public void IsSalesInitiated_PamOnPlanSupportsAndNoSubscriptionInfo_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrial.IsSalesInitiated(organization, new EnterprisePlan(isAnnual: true));

        Assert.True(result);
    }

    [Fact]
    public void IsSalesInitiated_PamOnAndSubscriptionHasNoPamLine_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrial.IsSalesInitiated(
            organization, new EnterprisePlan(isAnnual: true), SubscriptionInfoWithPamLine(false));

        Assert.True(result);
    }

    [Fact]
    public void IsSalesInitiated_SubscriptionHasPamLine_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrial.IsSalesInitiated(
            organization, new EnterprisePlan(isAnnual: true), SubscriptionInfoWithPamLine(true));

        Assert.False(result);
    }

    [Fact]
    public void IsSalesInitiated_PamOff_ReturnsFalse()
    {
        var organization = new Organization { UsePam = false };

        var result = PrivilegedControlsTrial.IsSalesInitiated(organization, new EnterprisePlan(isAnnual: true));

        Assert.False(result);
    }

    [Fact]
    public void IsSalesInitiated_PlanDoesNotSupportPrivilegedControls_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        var result = PrivilegedControlsTrial.IsSalesInitiated(organization, new TeamsPlan(isAnnual: true));

        Assert.False(result);
    }

    private static SubscriptionInfo SubscriptionInfoWithPamLine(bool hasPamLine) => new()
    {
        Subscription = new SubscriptionInfo.BillingSubscription(null!) { HasPrivilegedControlsSeats = hasPamLine }
    };
}

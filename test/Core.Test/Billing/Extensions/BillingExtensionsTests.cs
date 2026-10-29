using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Core.Test.Billing.Extensions;

public class BillingExtensionsTests
{
    [Fact]
    public void IsOnPrivilegedControlsSalesInitiatedTrial_PlanWithoutPrivilegedControls_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        Assert.False(organization.IsOnPrivilegedControlsSalesInitiatedTrial(new TeamsPlan(isAnnual: true), false));
    }

    [Fact]
    public void IsOnPrivilegedControlsSalesInitiatedTrial_PrivilegedControlsOff_ReturnsFalse()
    {
        var organization = new Organization { UsePam = false };

        Assert.False(organization.IsOnPrivilegedControlsSalesInitiatedTrial(new EnterprisePlan(isAnnual: true), false));
    }

    [Fact]
    public void IsOnPrivilegedControlsSalesInitiatedTrial_SubscriptionHasSeats_ReturnsFalse()
    {
        var organization = new Organization { UsePam = true };

        Assert.False(organization.IsOnPrivilegedControlsSalesInitiatedTrial(new EnterprisePlan(isAnnual: true), true));
    }

    [Fact]
    public void IsOnPrivilegedControlsSalesInitiatedTrial_SubscriptionHasNoSeats_ReturnsTrue()
    {
        var organization = new Organization { UsePam = true };

        Assert.True(organization.IsOnPrivilegedControlsSalesInitiatedTrial(new EnterprisePlan(isAnnual: true), false));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, 10, true)]
    [InlineData(10, null, true)]
    [InlineData(10, 10, true)]
    [InlineData(11, 10, false)]
    public void PrivilegedControlsAutoscaleLimitFits(int? privilegedControlsLimit, int? passwordManagerLimit, bool expected)
    {
        Assert.Equal(expected,
            BillingExtensions.PrivilegedControlsAutoscaleLimitFits(privilegedControlsLimit, passwordManagerLimit));
    }
}

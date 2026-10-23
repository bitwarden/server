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

    [Fact]
    public void GetPrivilegedControlsSeatMinimum_OrganizationMinimumSet_ReturnsOrganizationMinimum()
    {
        var organization = new Organization { PamSeatMinimum = 4 };

        Assert.Equal(4, organization.GetPrivilegedControlsSeatMinimum(new EnterprisePlan(isAnnual: true)));
    }

    [Fact]
    public void GetPrivilegedControlsSeatMinimum_OrganizationMinimumNull_ReturnsPlanDefault()
    {
        var organization = new Organization { PamSeatMinimum = null };

        Assert.Equal(10, organization.GetPrivilegedControlsSeatMinimum(new EnterprisePlan(isAnnual: true)));
    }

    [Fact]
    public void GetPrivilegedControlsSeatMinimum_PlanWithoutPrivilegedControls_ThrowsArgumentException()
    {
        var organization = new Organization { PamSeatMinimum = null };

        var exception = Assert.Throws<ArgumentException>(
            () => organization.GetPrivilegedControlsSeatMinimum(new TeamsPlan(isAnnual: true)));

        Assert.Contains("TeamsAnnually", exception.Message);
    }
}

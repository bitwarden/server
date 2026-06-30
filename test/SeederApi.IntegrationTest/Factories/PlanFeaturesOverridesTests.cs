using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class PlanFeaturesOverridesTests
{
    private static Organization FreeOrg()
    {
        var org = new Organization();
        PlanFeatures.Apply(org, PlanType.Free);
        return org;
    }

    [Fact]
    public void ApplyOrganizationOverrides_Null_LeavesPlanDefaults()
    {
        var org = FreeOrg();

        PlanFeatures.ApplyOrganizationOverrides(org, null);

        // Free plan disables these capabilities.
        Assert.False(org.UseSso);
        Assert.False(org.UseGroups);
        Assert.False(org.UsePolicies);
    }

    [Fact]
    public void ApplyOrganizationOverrides_EnablesFlagOnTopOfPlanDefault()
    {
        var org = FreeOrg();

        PlanFeatures.ApplyOrganizationOverrides(org, new OrganizationOverrides { UseSso = true });

        Assert.True(org.UseSso);
    }

    [Fact]
    public void ApplyOrganizationOverrides_NullFlag_LeavesThatPlanDefaultUnchanged()
    {
        var org = FreeOrg();

        // Only override UseSso; UseGroups should keep the Free plan default (false).
        PlanFeatures.ApplyOrganizationOverrides(org, new OrganizationOverrides { UseSso = true });

        Assert.True(org.UseSso);
        Assert.False(org.UseGroups);
    }

    [Fact]
    public void ApplyOrganizationOverrides_CanDisableFlagEnabledByPlan()
    {
        var org = new Organization();
        PlanFeatures.Apply(org, PlanType.EnterpriseAnnually);
        Assert.True(org.UseGroups);

        PlanFeatures.ApplyOrganizationOverrides(org, new OrganizationOverrides { UseGroups = false });

        Assert.False(org.UseGroups);
    }

    [Fact]
    public void ApplyBilling_SetsGatewayFields()
    {
        var org = FreeOrg();

        OrganizationSeeder.ApplyBilling(org, GatewayType.Stripe, "cus_123", "sub_456");

        Assert.Equal(GatewayType.Stripe, org.Gateway);
        Assert.Equal("cus_123", org.GatewayCustomerId);
        Assert.Equal("sub_456", org.GatewaySubscriptionId);
    }

    [Fact]
    public void ApplyBilling_NullValues_LeaveFieldsUnchanged()
    {
        var org = FreeOrg();
        org.Gateway = GatewayType.Braintree;
        org.GatewayCustomerId = "existing";

        OrganizationSeeder.ApplyBilling(org, null, null, "sub_only");

        Assert.Equal(GatewayType.Braintree, org.Gateway);
        Assert.Equal("existing", org.GatewayCustomerId);
        Assert.Equal("sub_only", org.GatewaySubscriptionId);
    }
}

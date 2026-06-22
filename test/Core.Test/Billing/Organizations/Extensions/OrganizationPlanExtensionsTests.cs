using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Extensions;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Extensions;

public class OrganizationPlanExtensionsTests
{
    [Fact]
    public void ChangePlan_EnterpriseAnnually2020ToEnterpriseAnnually_AppliesStructuralFields()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            Plan = "Enterprise (Annually) 2020"
        };
        var targetPlan = new EnterprisePlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.Equal("Enterprise (Annually)", organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_EnterpriseMonthly2020ToEnterpriseMonthly_AppliesStructuralFields()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseMonthly2020,
            Plan = "Enterprise (Monthly) 2020"
        };
        var targetPlan = new EnterprisePlan(isAnnual: false);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.EnterpriseMonthly, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_EnterpriseAnnually2019ToEnterpriseAnnually_AppliesStructuralFields()
    {
        // Enterprise 2019 -> Current adds and removes NO capability flags (the 2019 set is
        // already full). This asserts every flag the helper writes lands on the current value,
        // i.e. nothing in active use is dropped.
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2019,
            Plan = "Enterprise (Annually) 2019"
        };
        var targetPlan = new EnterprisePlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.EnterpriseAnnually, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_EnterpriseMonthly2019ToEnterpriseMonthly_AppliesStructuralFields()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseMonthly2019,
            Plan = "Enterprise (Monthly) 2019"
        };
        var targetPlan = new EnterprisePlan(isAnnual: false);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.EnterpriseMonthly, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_TeamsAnnually2020ToTeamsAnnually_AppliesStructuralFields()
    {
        // Headline capability gain for Teams Track A: UseScim flips false -> true.
        var organization = new Organization
        {
            PlanType = PlanType.TeamsAnnually2020,
            Plan = "Teams (Annually) 2020",
            UseScim = false
        };
        var targetPlan = new TeamsPlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.TeamsAnnually, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_TeamsMonthly2020ToTeamsMonthly_AppliesStructuralFields()
    {
        var organization = new Organization
        {
            PlanType = PlanType.TeamsMonthly2020,
            Plan = "Teams (Monthly) 2020",
            UseScim = false
        };
        var targetPlan = new TeamsPlan(isAnnual: false);

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.TeamsMonthly, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_FamiliesAnnually2019ToFamiliesAnnually_AppliesStructuralFields()
    {
        // Regression: confirms ChangePlan produces the same structural projection that
        // UpgradePlanAsync's inline copy did before extraction, for a Families source/target.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually2019,
            Plan = "Families 2019"
        };
        var targetPlan = new FamiliesPlan();

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.FamiliesAnnually, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.HasGroups, organization.UseGroups);
        Assert.Equal(targetPlan.HasDirectory, organization.UseDirectory);
        Assert.Equal(targetPlan.HasEvents, organization.UseEvents);
        Assert.Equal(targetPlan.HasTotp, organization.UseTotp);
        Assert.Equal(targetPlan.Has2fa, organization.Use2fa);
        Assert.Equal(targetPlan.HasApi, organization.UseApi);
        Assert.Equal(targetPlan.HasSelfHost, organization.SelfHost);
        Assert.Equal(targetPlan.HasPolicies, organization.UsePolicies);
        Assert.Equal(targetPlan.HasMyItems, organization.UseMyItems);
        Assert.Equal(targetPlan.HasInviteLinks, organization.UseInviteLinks);
        Assert.Equal(targetPlan.HasSso, organization.UseSso);
        Assert.Equal(targetPlan.HasOrganizationDomains, organization.UseOrganizationDomains);
        Assert.Equal(targetPlan.HasScim, organization.UseScim);
        Assert.Equal(targetPlan.HasResetPassword, organization.UseResetPassword);
        Assert.Equal(targetPlan.HasCustomPermissions, organization.UseCustomPermissions);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_FamiliesAnnually2025ToFamiliesAnnually_AppliesStructuralFields()
    {
        // Regression: same as the 2019 case, for the 2025 Families source.
        var organization = new Organization
        {
            PlanType = PlanType.FamiliesAnnually2025,
            Plan = "Families 2025"
        };
        var targetPlan = new FamiliesPlan();

        organization.ChangePlan(targetPlan);

        Assert.Equal(PlanType.FamiliesAnnually, organization.PlanType);
        Assert.Equal(targetPlan.Name, organization.Plan);
        Assert.Equal(targetPlan.UsersGetPremium, organization.UsersGetPremium);
        Assert.Equal(targetPlan.PasswordManager.MaxCollections, organization.MaxCollections);
        Assert.True(organization.UsePasswordManager);
    }

    [Fact]
    public void ChangePlan_TargetHasKeyConnector_OrgHasKeyConnector_Preserves()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            UseKeyConnector = true
        };
        var targetPlan = new EnterprisePlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.True(organization.UseKeyConnector);
    }

    [Fact]
    public void ChangePlan_TargetHasKeyConnector_OrgDoesNot_StaysOff()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            UseKeyConnector = false
        };
        var targetPlan = new EnterprisePlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.False(organization.UseKeyConnector);
    }

    [Fact]
    public void ChangePlan_TargetDoesNotHaveKeyConnector_DisablesOnOrg()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            UseKeyConnector = true
        };
        var targetPlan = new TeamsPlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.False(organization.UseKeyConnector);
    }

    [Fact]
    public void ChangePlan_DoesNotMutateCustomerPurchaseColumns()
    {
        var organization = new Organization
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            Seats = 200,
            MaxStorageGb = (short)50,
            SmSeats = 25,
            SmServiceAccounts = 10,
            BusinessName = "Acme Inc.",
            Enabled = true,
            UseSecretsManager = true,
            MaxAutoscaleSeats = 500,
            MaxAutoscaleSmSeats = 100,
            MaxAutoscaleSmServiceAccounts = 50
        };
        var targetPlan = new EnterprisePlan(isAnnual: true);

        organization.ChangePlan(targetPlan);

        Assert.Equal(200, organization.Seats);
        Assert.Equal((short)50, organization.MaxStorageGb);
        Assert.Equal(25, organization.SmSeats);
        Assert.Equal(10, organization.SmServiceAccounts);
        Assert.Equal("Acme Inc.", organization.BusinessName);
        Assert.True(organization.Enabled);
        Assert.True(organization.UseSecretsManager);
        Assert.Equal(500, organization.MaxAutoscaleSeats);
        Assert.Equal(100, organization.MaxAutoscaleSmSeats);
        Assert.Equal(50, organization.MaxAutoscaleSmServiceAccounts);
    }
}

using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateOrganizationStepTests
{
    [Fact]
    public void NoOverride_FromParams_UsesProvidedName()
    {
        var context = NewContext(new SeederSettings());
        var step = CreateOrganizationStep.FromParams(TestOrgName, TestDomain);

        step.Execute(context);

        Assert.NotNull(context.Organization);
        Assert.Equal(TestOrgName, context.Organization!.Name);
    }

    [Fact]
    public void WithOrgNameOverride_FromParams_UsesOverrideName()
    {
        var context = NewContext(new SeederSettings(OrgNameOverride: "Override Org"));
        var step = CreateOrganizationStep.FromParams(TestOrgName, TestDomain);

        step.Execute(context);

        Assert.Equal("Override Org", context.Organization!.Name);
    }

    [Fact]
    public void WhitespaceOverride_IgnoredAndFixtureNameWins()
    {
        var context = NewContext(new SeederSettings(OrgNameOverride: "   "));
        var step = CreateOrganizationStep.FromParams(TestOrgName, TestDomain);

        step.Execute(context);

        Assert.Equal(TestOrgName, context.Organization!.Name);
    }

    [Fact]
    public void WithOrgNameOverride_FromFixture_UsesOverrideName()
    {
        var reader = new StubSeedReader()
            .Add("organizations.acme", new SeedOrganization { Name = "Acme From Fixture", Domain = TestDomain });
        var context = NewContext(new SeederSettings(OrgNameOverride: "Renamed Acme"), reader);

        var step = CreateOrganizationStep.FromFixture("acme");
        step.Execute(context);

        Assert.Equal("Renamed Acme", context.Organization!.Name);
        Assert.Equal(TestDomain, context.Domain);
    }
}

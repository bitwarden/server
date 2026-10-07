using System.Text.RegularExpressions;
using Bit.Core.Enums;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateOwnerStepTests
{
    [Fact]
    public void NoOverride_OwnerEmailIsOwnerAtDomain()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);

        new CreateOwnerStep().Execute(context);

        Assert.NotNull(context.Owner);
        Assert.Equal($"owner@{TestDomain}", context.Owner!.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_OwnerEmailIsOverride()
    {
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "jared@bw.example"));
        PreloadOrganization(context);

        new CreateOwnerStep().Execute(context);

        Assert.Equal("jared@bw.example", context.Owner!.Email);
    }

    [Fact]
    public void WithOwnerEmailOverride_AndManglingEnabled_OverrideIsMangledNotLiteral()
    {
        var mangler = new ManglerService();
        var context = NewContextWithMangler(
            new SeederSettings(OwnerEmailOverride: "jared@bw.example"),
            mangler);
        PreloadOrganization(context);

        new CreateOwnerStep().Execute(context);

        // Mangler prepends `{8hexChars}+` to the local part; original literal is gone.
        Assert.NotEqual("jared@bw.example", context.Owner!.Email);
        Assert.Matches(new Regex(@"^[a-f0-9]{8}\+jared@bw\.example$"), context.Owner.Email);
    }

    [Fact]
    public void NoOverride_AndManglingEnabled_DefaultOwnerEmailIsMangled()
    {
        // Sanity baseline: without an override, the default owner@<domain> still gets mangled
        // when mangling is enabled. Asserts our mangle interaction logic isn't override-specific.
        var mangler = new ManglerService();
        var context = NewContextWithMangler(new SeederSettings(), mangler);
        PreloadOrganization(context);

        new CreateOwnerStep().Execute(context);

        Assert.Matches(new Regex($@"^[a-f0-9]{{8}}\+owner@{Regex.Escape(TestDomain)}$"), context.Owner!.Email);
    }

    [Fact]
    public void LinksOwnerOrgUserToOrganizationAsConfirmedOwner()
    {
        var context = NewContext(new SeederSettings(OwnerEmailOverride: "jared@bw.example"));
        PreloadOrganization(context);

        new CreateOwnerStep().Execute(context);

        Assert.NotNull(context.OwnerOrgUser);
        Assert.Equal(context.Organization!.Id, context.OwnerOrgUser!.OrganizationId);
        Assert.Equal(OrganizationUserType.Owner, context.OwnerOrgUser.Type);
        Assert.Equal(OrganizationUserStatusType.Confirmed, context.OwnerOrgUser.Status);
    }
}

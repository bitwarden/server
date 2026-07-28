using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class CreateSsoConfigStepTests
{
    private static SeederContext BuildContext(Organization organization)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IManglerService, NoOpManglerService>();
        services.AddLogging();
        return new SeederContext(services.BuildServiceProvider()) { Organization = organization };
    }

    // The SAML path fetches the cert from the live IdP, so it is exercised end-to-end (M6/M7),
    // not here. These cover the guards that don't need a running IdP.

    [Fact]
    public void Execute_NonSamlProvider_SkipsWithoutMutating()
    {
        var org = new Organization { Id = Guid.NewGuid(), Identifier = "unchanged" };
        var context = BuildContext(org);
        var step = new CreateSsoConfigStep("local-sso", "oidc", MemberDecryptionType.MasterPassword);

        step.Execute(context);

        Assert.Empty(context.SsoConfigs);
        Assert.Equal("unchanged", org.Identifier);
    }

    [Fact]
    public void WithSso_WithoutOrganization_Throws()
    {
        var builder = new ServiceCollection().AddRecipe("test");

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.WithSso("local-sso", "saml", MemberDecryptionType.MasterPassword));
        Assert.Contains("requires an organization", ex.Message);
    }

    [Fact]
    public void WithSso_WithEmptyIdentifier_Throws()
    {
        var builder = new ServiceCollection().AddRecipe("test");
        builder.HasOrg = true;

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.WithSso("   ", "saml", MemberDecryptionType.MasterPassword));
        Assert.Contains("non-empty identifier", ex.Message);
    }
}

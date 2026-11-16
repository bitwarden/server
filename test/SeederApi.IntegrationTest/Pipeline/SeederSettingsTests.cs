using Bit.Core.Entities;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

public class SeederSettingsTests
{
    [Fact]
    public void Defaults_OverridesAreNull()
    {
        var settings = new SeederSettings();
        Assert.Null(settings.OrgNameOverride);
        Assert.Null(settings.OwnerEmailOverride);
    }

    [Fact]
    public void ContextExtensions_ReadOverridesFromSettings()
    {
        var context = NewContext(new SeederSettings(
            Password: null,
            KdfIterations: 5_000,
            OrgNameOverride: "OrgFromSettings",
            OwnerEmailOverride: "owner-from-settings@bw.example"));

        Assert.Equal("OrgFromSettings", context.OrgNameOverride);
        Assert.Equal("owner-from-settings@bw.example", context.OwnerEmailOverride);
    }

    [Fact]
    public void ContextExtensions_NoOverrides_ReturnsNull()
    {
        var context = NewContext(new SeederSettings());
        Assert.Null(context.OrgNameOverride);
        Assert.Null(context.OwnerEmailOverride);
    }

    private static SeederContext NewContext(SeederSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IManglerService, NoOpManglerService>();
        services.AddSingleton(settings);
        return new SeederContext(services.BuildServiceProvider());
    }
}

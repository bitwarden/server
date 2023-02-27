using AutoFixture;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LaunchDarklyFeatureServiceTests
{
    public static SutProvider<LaunchDarklyFeatureService> GetSutProvider(Core.Settings.GlobalSettings globalSettings)
    {
        var fixture = new Fixture();
        return new SutProvider<LaunchDarklyFeatureService>(fixture)
            .SetDependency<Core.Settings.GlobalSettings>(globalSettings)
            .Create();
    }

    [Fact]
    public void Online_WhenNotSelfHost()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings() { SelfHosted = false });

        Assert.True(sutProvider.Sut.IsOnline());
    }

    [Fact]
    public void Offline_WhenSelfHost()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings() { SelfHosted = true });

        Assert.False(sutProvider.Sut.IsOnline());
    }

    [Fact]
    public void Online_WithFileFallback_WhenSdkKeyNull()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings());

        Assert.True(sutProvider.Sut.IsOnline());
    }
}

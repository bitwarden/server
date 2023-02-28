using AutoFixture;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LaunchDarklyFeatureServiceTests
{
    public static SutProvider<LaunchDarklyFeatureService> GetSutProvider(IGlobalSettings globalSettings)
    {
        var fixture = new Fixture();
        return new SutProvider<LaunchDarklyFeatureService>(fixture)
            .SetDependency<IGlobalSettings>(globalSettings)
            .Create();
    }

    [Fact]
    public void Offline_WhenSelfHost()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings() { SelfHosted = true });

        Assert.False(sutProvider.Sut.IsOnline());
    }
}

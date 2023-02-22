using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LaunchDarklyFeatureServiceTests
{
    [Theory(Skip = "For local development"), SelfHostedAutoData(false), BitAutoData]
    public void Online_WhenNotSelfHost(SutProvider<LaunchDarklyFeatureService> sutProvider)
    {
        Assert.True(sutProvider.Sut.IsOnline());
    }

    [Theory(Skip = "For local development"), SelfHostedAutoData(true), BitAutoData]
    public void Offline_WhenSelfHost(SutProvider<LaunchDarklyFeatureService> sutProvider)
    {
        Assert.False(sutProvider.Sut.IsOnline());
    }
}

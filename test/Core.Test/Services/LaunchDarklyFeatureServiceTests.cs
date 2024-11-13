using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using LaunchDarkly.Sdk.Server.Interfaces;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LaunchDarklyFeatureServiceTests
{
    private const string _fakeFeatureKey = "somekey";
    private const string _fakeSdkKey = "somesdkkey";

    private static SutProvider<LaunchDarklyFeatureService> GetSutProvider(IGlobalSettings globalSettings)
    {
        globalSettings.ProjectName = "LaunchDarkly Tests";

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());
        currentContext.ClientVersion.Returns(new Version(AssemblyHelpers.GetVersion()));
        currentContext.ClientVersionIsPrerelease.Returns(true);
        currentContext.DeviceType.Returns(Enums.DeviceType.ChromeBrowser);

        var client = Substitute.For<ILdClient>();

        var fixture = new Fixture();
        return new SutProvider<LaunchDarklyFeatureService>(fixture)
            .SetDependency(globalSettings)
            .SetDependency(currentContext)
            .SetDependency(client)
            .Create();
    }

    [Theory, BitAutoData]
    public void DefaultFeatureValue_WhenSelfHost(string key)
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings { SelfHosted = true });

        Assert.False(sutProvider.Sut.IsEnabled(key));
    }

    [Fact]
    public void DefaultFeatureValue_NoSdkKey()
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings());

        Assert.False(sutProvider.Sut.IsEnabled(_fakeFeatureKey));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Boolean()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        Assert.False(sutProvider.Sut.IsEnabled(_fakeFeatureKey));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Int()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        Assert.Equal(0, sutProvider.Sut.GetIntVariation(_fakeFeatureKey));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_String()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        Assert.Null(sutProvider.Sut.GetStringVariation(_fakeFeatureKey));
    }

    [Fact(Skip = "For local development")]
    public void GetAll()
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings());

        var results = sutProvider.Sut.GetAll();

        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }
}

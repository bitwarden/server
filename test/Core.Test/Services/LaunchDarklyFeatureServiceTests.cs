using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
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

        var fixture = new Fixture();
        return new SutProvider<LaunchDarklyFeatureService>(fixture)
            .SetDependency(globalSettings)
            .Create();
    }

    [Theory, BitAutoData]
    public void DefaultFeatureValue_WhenSelfHost(string key)
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings { SelfHosted = true });

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled(key, currentContext));
    }

    [Fact]
    public void DefaultFeatureValue_NoSdkKey()
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings());

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled(_fakeFeatureKey, currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Boolean()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled(_fakeFeatureKey, currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Int()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.Equal(0, sutProvider.Sut.GetIntVariation(_fakeFeatureKey, currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_String()
    {
        var settings = new Settings.GlobalSettings { LaunchDarkly = { SdkKey = _fakeSdkKey } };

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.Null(sutProvider.Sut.GetStringVariation(_fakeFeatureKey, currentContext));
    }

    [Fact(Skip = "For local development")]
    public void GetAll()
    {
        var sutProvider = GetSutProvider(new Settings.GlobalSettings());

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        var results = sutProvider.Sut.GetAll(currentContext);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }
}

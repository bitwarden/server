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

    [Theory, BitAutoData]
    public void DefaultFeatureValue_WhenSelfHost(string key)
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings() { SelfHosted = true });

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled(key, currentContext));
    }

    [Fact]
    public void DefaultFeatureValue_NoSdkKey()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings());

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled("somekey", currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Boolean()
    {
        var settings = new Core.Settings.GlobalSettings();
        settings.LaunchDarkly.SdkKey = "somevalue";

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.False(sutProvider.Sut.IsEnabled("somekey", currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_Int()
    {
        var settings = new Core.Settings.GlobalSettings();
        settings.LaunchDarkly.SdkKey = "somevalue";

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.Equal(0, sutProvider.Sut.GetIntVariation("somekey", currentContext));
    }

    [Fact(Skip = "For local development")]
    public void FeatureValue_String()
    {
        var settings = new Core.Settings.GlobalSettings();
        settings.LaunchDarkly.SdkKey = "somevalue";

        var sutProvider = GetSutProvider(settings);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        Assert.Null(sutProvider.Sut.GetStringVariation("somekey", currentContext));
    }

    [Fact(Skip = "For local development")]
    public void GetAll()
    {
        var sutProvider = GetSutProvider(new Core.Settings.GlobalSettings());

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(Guid.NewGuid());

        var results = sutProvider.Sut.GetAll(currentContext);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }
}

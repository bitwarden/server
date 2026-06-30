using Bit.Api.Models.Response;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class ServerSettingsResponseModelTests
{
    [Fact]
    public void ConfigResponseModel_SuppressOnboardingInterstitialsTrue_MapsToSettings()
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.BaseServiceUri.Returns(Substitute.For<IBaseServiceUriSettings>());
        globalSettings.DisableUserRegistration.Returns(false);
        globalSettings.SuppressOnboardingInterstitials.Returns(true);
        globalSettings.WebPush.Returns(Substitute.For<IWebPushSettings>());

        var featureService = Substitute.For<IFeatureService>();
        featureService.GetAll().Returns(new Dictionary<string, object>());

        var model = new ConfigResponseModel(featureService, globalSettings);

        Assert.True(model.Settings.SuppressOnboardingInterstitials);
    }

    [Fact]
    public void ConfigResponseModel_SuppressOnboardingInterstitialsFalse_MapsToSettings()
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.BaseServiceUri.Returns(Substitute.For<IBaseServiceUriSettings>());
        globalSettings.DisableUserRegistration.Returns(false);
        globalSettings.SuppressOnboardingInterstitials.Returns(false);
        globalSettings.WebPush.Returns(Substitute.For<IWebPushSettings>());

        var featureService = Substitute.For<IFeatureService>();
        featureService.GetAll().Returns(new Dictionary<string, object>());

        var model = new ConfigResponseModel(featureService, globalSettings);

        Assert.False(model.Settings.SuppressOnboardingInterstitials);
    }
}

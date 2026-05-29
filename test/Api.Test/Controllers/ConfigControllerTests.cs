using AutoFixture.Xunit2;
using Bit.Api.Controllers;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

public class ConfigControllerTests : IDisposable
{
    private readonly ConfigController _sut;
    private readonly GlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;

    public ConfigControllerTests()
    {
        _globalSettings = new GlobalSettings();
        _featureService = Substitute.For<IFeatureService>();
        _featureService.GetAll().Returns(new Dictionary<string, object>());

        _sut = new ConfigController(
            _globalSettings,
            _featureService
        );
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public void GetConfigs_WithFeatureStates(Dictionary<string, object> featureStates)
    {
        _featureService.GetAll().Returns(featureStates);

        var response = _sut.GetConfigs();

        Assert.NotNull(response);
        Assert.NotNull(response.FeatureStates);
        Assert.Equal(featureStates, response.FeatureStates);
    }

    [Fact]
    public void GetConfigs_FillAssistRulesNotConfigured_ReturnsNullEnvironmentValue()
    {
        // BaseServiceUriSettings.FillAssistRules defaults to null when not explicitly set
        var response = _sut.GetConfigs();

        Assert.NotNull(response.Environment);
        Assert.Null(response.Environment.FillAssistRules);
    }

    [Fact]
    public void GetConfigs_FillAssistRulesConfigured_ReturnsConfiguredValue()
    {
        var expectedUri = "https://example.com/custom-rules.json";
        _globalSettings.BaseServiceUri.FillAssistRules = expectedUri;

        var response = _sut.GetConfigs();

        Assert.NotNull(response.Environment);
        Assert.Equal(expectedUri, response.Environment.FillAssistRules);
    }
}

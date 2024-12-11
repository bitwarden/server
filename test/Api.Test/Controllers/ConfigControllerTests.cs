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

        _sut = new ConfigController(_globalSettings, _featureService);
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
}

using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class DuoUniversalTokenServiceTests
{
    [Theory]
    [BitAutoData("", "ClientId", "ClientSecret")]
    [BitAutoData("api-valid.duosecurity.com", "", "ClientSecret")]
    [BitAutoData("api-valid.duosecurity.com", "ClientId", "")]
    public async void ValidateDuoConfiguration_InvalidConfig_ReturnsFalse(
        string host, string clientId, string clientSecret, SutProvider<DuoUniversalTokenService> sutProvider)
    {
        // Arrange
        /* AutoData handles arrangement */

        // Act
        var result = await sutProvider.Sut.ValidateDuoConfiguration(clientSecret, clientId, host);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(true, "api-valid.duosecurity.com")]
    [BitAutoData(false, "invalid")]
    [BitAutoData(false, "api-valid.duosecurity.com", null, "clientSecret")]
    [BitAutoData(false, "api-valid.duosecurity.com", "ClientId", null)]
    [BitAutoData(false, "api-valid.duosecurity.com", null, null)]
    public void HasProperDuoMetadata_ReturnMatchesExpected(
        bool expectedResponse, string host, string clientId,
        string clientSecret, SutProvider<DuoUniversalTokenService> sutProvider)
    {
        // Arrange
        var metaData = new Dictionary<string, object> { ["Host"] = host };

        if (clientId != null)
        {
            metaData.Add("ClientId", clientId);
        }

        if (clientSecret != null)
        {
            metaData.Add("ClientSecret", clientSecret);
        }

        var provider = new TwoFactorProvider
        {
            MetaData = metaData
        };

        // Act
        var result = sutProvider.Sut.HasProperDuoMetadata(provider);

        // Assert
        Assert.Equal(result, expectedResponse);
    }

    [Theory]
    [BitAutoData]
    public void HasProperDuoMetadata_ProviderIsNull_ReturnsFalse(
        SutProvider<DuoUniversalTokenService> sutProvider)
    {
        // Act
        var result = sutProvider.Sut.HasProperDuoMetadata(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData("api-valid.duosecurity.com", true)]
    [BitAutoData("api-valid.duofederal.com", true)]
    [BitAutoData("invalid", false)]
    public void ValidDuoHost_HostIsValid_ReturnTrue(
        string host, bool expectedResponse)
    {
        // Act
        var result = DuoUniversalTokenService.ValidDuoHost(host);

        // Assert
        Assert.Equal(result, expectedResponse);
    }


}

using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Auth.Services;

public class DuoUniversalConfigServiceTests
{
    private IDuoUniversalConfigService _sut;

    public DuoUniversalConfigServiceTests()
    {
        _sut = new DuoUniversalConfigService();
    }

    [Theory]
    [BitAutoData("", "ClientId", "ClientSecret")]
    [BitAutoData("api-valid.duosecurity.com", "", "ClientSecret")]
    [BitAutoData("api-valid.duosecurity.com", "ClientId", "")]
    public async void ValidateDuoConfiguration_InvalidConfig_ReturnsFalse(
        string host, string clientId, string clientSecret)
    {
        // Arrange
        /* AutoData handles arrangement */

        // Act
        var result = await _sut.ValidateDuoConfiguration(clientSecret, clientId, host);

        // Assert
        Assert.False(result);
    }
}

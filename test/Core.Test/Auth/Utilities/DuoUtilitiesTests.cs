using Bit.Core.Auth.Models;
using Bit.Core.Auth.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Auth.Utilities;

public class DuoUtilitiesTests
{
    [Theory]
    [BitAutoData(true, "api-valid.duosecurity.com")]
    [BitAutoData(false, "invalid")]
    [BitAutoData(false, "api-valid.duosecurity.com", null, "clientSecret")]
    [BitAutoData(false, "api-valid.duosecurity.com", "ClientId", null)]
    [BitAutoData(false, "api-valid.duosecurity.com", null, null)]
    public void HasProperDuoMetadata_ReturnsMatchesExpected(
        bool expectedResponse, string host, string clientId, string clientSecret)
    {
        // Arrange
        var provider = new TwoFactorProvider
        {
            MetaData = new Dictionary<string, object>
            {
                ["ClientId"] = clientId,
                ["ClientSecret"] = clientSecret,
                ["Host"] = host,
            }
        };

        // Act
        var result = DuoUtilities.HasProperDuoMetadata(provider);

        // Assert
        Assert.Equal(result, expectedResponse);
    }

    [Fact]
    public void HasProperDuoMetadata_ProviderIsNull_ReturnsMatchesExpected()
    {
        // Act
        var result = DuoUtilities.HasProperDuoMetadata(null);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData("api-valid.duosecurity.com", true)]
    [BitAutoData("api-valid.duofederal.com", true)]
    [BitAutoData("invalid", false)]
    public void ValidDuoHost_HostIsValid_ReturnTrue(string host, bool expectedResponse)
    {
        // Act
        var result = DuoUtilities.ValidDuoHost(host);

        // Assert
        Assert.Equal(result, expectedResponse);
    }
}

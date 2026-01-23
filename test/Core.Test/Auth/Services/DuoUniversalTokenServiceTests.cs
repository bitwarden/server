using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;
using CoreSettings = Bit.Core.Settings;

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

    [Theory]
    [BitAutoData("vault.bitwarden.com")] // Cloud US
    [BitAutoData("vault.bitwarden.eu")]  // Cloud EU
    public void BuildDuoTwoFactorRedirectUri_MobileClient_CloudHost_ReturnsHttpsScheme(
        string requestHost)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = "mobile";
        httpContext.Request.Host = new HostString(requestHost);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=mobile", result);
        Assert.Contains("deeplinkScheme=https", result);
        Assert.StartsWith("https://vault.bitwarden.com/duo-redirect-connector.html", result);
    }

    [Theory]
    [BitAutoData("selfhosted.example.com")]
    [BitAutoData("192.168.1.100")]
    public void BuildDuoTwoFactorRedirectUri_MobileClient_SelfHosted_ReturnsBitwardenScheme(
        string requestHost)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = "mobile";
        httpContext.Request.Host = new HostString(requestHost);

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.example.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=mobile", result);
        Assert.Contains("deeplinkScheme=bitwarden", result);
    }

    [Fact]
    public void BuildDuoTwoFactorRedirectUri_DesktopClient_ReturnsBitwardenScheme()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = "desktop";
        httpContext.Request.Host = new HostString("vault.bitwarden.com");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=desktop", result);
        Assert.Contains("deeplinkScheme=bitwarden", result);
    }

    [Theory]
    [BitAutoData("web")]
    [BitAutoData("browser")]
    [BitAutoData("cli")]
    public void BuildDuoTwoFactorRedirectUri_NonMobileNonDesktopClient_NoDeeplinkScheme(
        string clientName)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = clientName;
        httpContext.Request.Host = new HostString("vault.bitwarden.com");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains($"client={clientName}", result);
        Assert.DoesNotContain("deeplinkScheme", result);
    }

    [Fact]
    public void BuildDuoTwoFactorRedirectUri_NoClientHeader_DefaultsToWeb()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // No Bitwarden-Client-Name header set
        httpContext.Request.Host = new HostString("vault.bitwarden.com");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=web", result);
        Assert.DoesNotContain("deeplinkScheme", result);
    }

    [Theory]
    [BitAutoData("invalid-client")]
    [BitAutoData("unknown")]
    public void BuildDuoTwoFactorRedirectUri_InvalidClientHeader_DefaultsToWeb(
        string invalidClientName)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = invalidClientName;
        httpContext.Request.Host = new HostString("vault.bitwarden.com");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=web", result);
        Assert.DoesNotContain("deeplinkScheme", result);
    }

    [Theory]
    [BitAutoData("MOBILE")]
    [BitAutoData("Mobile")]
    [BitAutoData("MoBiLe")]
    public void BuildDuoTwoFactorRedirectUri_ClientHeaderCaseInsensitive(
        string clientName)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Bitwarden-Client-Name"] = clientName;
        httpContext.Request.Host = new HostString("vault.bitwarden.com");

        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.HttpContext.Returns(httpContext);

        var globalSettings = new CoreSettings.GlobalSettings
        {
            BaseServiceUri = new CoreSettings.GlobalSettings.BaseServiceUriSettings(new CoreSettings.GlobalSettings()) { Vault = "https://vault.bitwarden.com" }
        };

        var sut = new DuoUniversalTokenService(currentContext, globalSettings);

        // Act
        var result = sut.BuildDuoTwoFactorRedirectUri();

        // Assert
        Assert.Contains("client=mobile", result);
        Assert.Contains("deeplinkScheme=https", result);
    }
}

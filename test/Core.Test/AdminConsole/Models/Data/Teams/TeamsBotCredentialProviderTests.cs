using Bit.Core.AdminConsole.Models.Teams;
using Xunit;

namespace Bit.Core.Test.Models.Data.Teams;

public class TeamsBotCredentialProviderTests
{
    private string _clientId = "client id";
    private string _clientSecret = "client secret";
    private string _microsoftTeamsBotIssuer = "https://api.botframework.com";

    [Fact]
    public async Task IsValidAppId_MustMatchClientId()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);

        Assert.True(await sut.IsValidAppIdAsync(_clientId));
        Assert.False(await sut.IsValidAppIdAsync("Different id"));
    }

    [Fact]
    public async Task GetAppPasswordAsync_MatchingClientId_ReturnsClientSecret()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        var password = await sut.GetAppPasswordAsync(_clientId);
        Assert.Equal(_clientSecret, password);
    }

    [Fact]
    public async Task GetAppPasswordAsync_NotMatchingClientId_ReturnsNull()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        Assert.Null(await sut.GetAppPasswordAsync("Different id"));
    }

    [Fact]
    public async Task IsAuthenticationDisabledAsync_ReturnsFalse()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        Assert.False(await sut.IsAuthenticationDisabledAsync());
    }

    [Fact]
    public async Task ValidateIssuerAsync_ExpectedIssuer_ReturnsTrue()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        Assert.True(await sut.ValidateIssuerAsync(_microsoftTeamsBotIssuer));
    }

    [Fact]
    public async Task ValidateIssuerAsync_UnexpectedIssuer_ReturnsFalse()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        Assert.False(await sut.ValidateIssuerAsync("unexpected issuer"));
    }
}

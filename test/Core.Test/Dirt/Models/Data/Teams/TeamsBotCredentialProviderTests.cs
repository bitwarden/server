using Bit.Core.Dirt.Models.Data.Teams;
using Microsoft.Bot.Connector.Authentication;
using Xunit;

namespace Bit.Core.Test.Dirt.Models.Data.Teams;

public class TeamsBotCredentialProviderTests
{
    private string _clientId = "client id";
    private string _clientSecret = "client secret";

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
        Assert.True(await sut.ValidateIssuerAsync(AuthenticationConstants.ToBotFromChannelTokenIssuer));
    }

    [Fact]
    public async Task ValidateIssuerAsync_UnexpectedIssuer_ReturnsFalse()
    {
        var sut = new TeamsBotCredentialProvider(_clientId, _clientSecret);
        Assert.False(await sut.ValidateIssuerAsync("unexpected issuer"));
    }
}

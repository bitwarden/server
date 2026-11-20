using Bit.Core.Enums;
using Bit.IntegrationTestCommon.Factories;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.RefreshTokenGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for the <c>refresh_token</c> requests that the token endpoint rejects.
/// </summary>
public class RefreshTokenGrantRejectionTests
{
    /// <summary>
    /// A refresh token that the server did not issue, such as a changed copy of a real token, is rejected.
    /// </summary>
    [Fact]
    public async Task RefreshToken_Unknown_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        // A real grant exists, so the rejection depends only on the token value.
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);
        var changedToken = refreshToken[..^1] + (refreshToken[^1] == 'A' ? 'B' : 'A');

        var context = await PostRefreshTokenAsync(factory, changedToken);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A refresh token belongs to the client that it was issued to. No other client can use it.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, BitwardenClient.Browser)]
    [InlineData(BitwardenClient.Browser, BitwardenClient.Desktop)]
    [InlineData(BitwardenClient.Desktop, BitwardenClient.Mobile)]
    [InlineData(BitwardenClient.Mobile, BitwardenClient.Cli)]
    [InlineData(BitwardenClient.Cli, BitwardenClient.DirectoryConnector)]
    [InlineData(BitwardenClient.DirectoryConnector, BitwardenClient.Web)]
    public async Task RefreshToken_SentByAnotherClient_IsRejected(string issuedToClientId, string sentByClientId)
    {
        using var factory = new IdentityApplicationFactory();
        var (_, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory, issuedToClientId);

        var context = await PostRefreshTokenAsync(factory, refreshToken, sentByClientId);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A refresh request without a refresh token is rejected as a malformed request. An empty or blank value counts
    /// as missing.
    /// </summary>
    [Theory]
    [InlineData(null)] // field omitted
    [InlineData("")]
    [InlineData(" ")]
    public async Task RefreshToken_MissingOrBlank_IsRejected(string? refreshToken)
    {
        using var factory = new IdentityApplicationFactory();
        var form = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", BitwardenClient.Web },
        };
        if (refreshToken != null)
        {
            form.Add("refresh_token", refreshToken);
        }

        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(form));

        await AssertErrorAsync(context, "invalid_request");
    }

    /// <summary>
    /// Actions such as a password change or "Deauthorize sessions" rotate the user's security stamp. The refresh token
    /// carries the stamp from the login, so a rotated stamp ends the session.
    /// </summary>
    [Fact]
    public async Task RefreshToken_AfterSecurityStampChange_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, _, refreshToken) = await RegisterAndLoginWithPasswordAsync(factory);

        // The token works before the change, so the later rejection comes from the new stamp only.
        var beforeChangeContext = await PostRefreshTokenAsync(factory, refreshToken);
        using var beforeChangeBody = await AssertDefaultTokenBodyAsync(beforeChangeContext);

        // A direct stamp write keeps the test about the stamp check, not about any one action that rotates the stamp.
        await UpdateUserAsync(factory, user.Id, u => u.SecurityStamp = Guid.NewGuid().ToString());
        var afterChangeContext = await PostRefreshTokenAsync(factory, refreshToken);

        await AssertInvalidGrantAsync(afterChangeContext);
    }
}

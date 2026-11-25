using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AuthorizationCodeGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.AuthorizationCode;

/// <summary>
/// Integration tests for <c>authorization_code</c> grant requests that the token endpoint rejects. The tests store the
/// authorization code directly instead of running the SSO login, so they cover only the token request.
/// </summary>
public class AuthorizationCodeGrantRejectionTests
{
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A code that the server did not issue is rejected.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_UnknownCode_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, "unknown_code");

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A request without a code is rejected.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_MissingCode_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code: null);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A code is valid for 5 minutes after the SSO login issues it.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_ExpiredCode_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory,
            c => c.CreationTime = DateTime.UtcNow - AuthorizationCodeLifetime - TimeSpan.FromMinutes(1));

        var context = await PostAuthorizationCodeAsync(factory, code);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A code works only for the client that the SSO login issued it to.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_CodeSentByAnotherClient_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code, clientId: BitwardenClient.Desktop);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// The request must send the redirect URI that the SSO login sent.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_OtherRedirectUri_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code,
            redirectUri: "https://localhost:8080/other-connector.html");

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A request without a redirect URI is rejected.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_MissingRedirectUri_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code, redirectUri: null);

        await AssertErrorAsync(context, "unauthorized_client");
    }

    /// <summary>
    /// The PKCE code verifier must match the code challenge that the SSO login sent.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_WrongCodeVerifier_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        const string wrongCodeVerifier = "wrong_code_verifier_of_valid_length_0123456789_0123";
        Assert.Equal(DefaultCodeVerifier.Length, wrongCodeVerifier.Length);

        var context = await PostAuthorizationCodeAsync(factory, code, codeVerifier: wrongCodeVerifier);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A request without a PKCE code verifier is rejected.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_MissingCodeVerifier_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code, codeVerifier: null);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// Every client that can use the grant requires PKCE, so a code issued without a code challenge is rejected.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_CodeWithoutCodeChallenge_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory, c =>
        {
            c.CodeChallenge = null;
            c.CodeChallengeMethod = null;
        });

        var context = await PostAuthorizationCodeAsync(factory, code, codeVerifier: null);

        await AssertInvalidGrantAsync(context);
    }

    /// <summary>
    /// A code for a user who was deleted after the SSO login is rejected. The response has no error description.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_DeletedUser_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (user, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        await userRepository.DeleteAsync(user);
        Assert.Null(await userRepository.GetByIdAsync(user.Id));

        var context = await PostAuthorizationCodeAsync(factory, code);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        Assert.Equal(new[] { "ErrorModel", "error" },
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("invalid_grant", AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString());
        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("Username or password is incorrect. Try again.",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
    }

    /// <summary>
    /// The Send client cannot use the grant.
    /// </summary>
    [Fact]
    public async Task AuthorizationCodeGrant_SendClient_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var (_, code) = await RegisterSsoUserWithAuthorizationCodeAsync(factory);

        var context = await PostAuthorizationCodeAsync(factory, code, clientId: BitwardenClient.Send);

        await AssertErrorAsync(context, "unauthorized_client");
    }
}

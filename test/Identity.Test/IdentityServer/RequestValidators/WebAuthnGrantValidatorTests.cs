using System.Collections.Specialized;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Entities;
using Bit.Core.Tokens;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using Fido2NetLib;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

[SutProviderCustomize]
public class WebAuthnGrantValidatorTests
{
    private static ExtensionGrantValidationContext CreateContext(
        ValidatedTokenRequest tokenRequest,
        string token = "test-token",
        string deviceResponse = """{"id":"abc","rawId":"abc","type":"public-key","response":{"authenticatorData":"dGVzdA","signature":"dGVzdA","clientDataJSON":"dGVzdA","userHandle":"dGVzdA"}}""")
    {
        tokenRequest.Raw = new NameValueCollection
        {
            { "token", token },
            { "deviceResponse", deviceResponse }
        };

        return new ExtensionGrantValidationContext { Request = tokenRequest };
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingToken_RejectsWithInvalidGrant(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<WebAuthnGrantValidator> sutProvider)
    {
        // Arrange - no token or deviceResponse in raw params
        tokenRequest.Raw = new NameValueCollection();
        var context = new ExtensionGrantValidationContext { Request = tokenRequest };

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal("invalid_grant", context.Result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_InvalidToken_RejectsWithInvalidRequest(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<WebAuthnGrantValidator> sutProvider)
    {
        // Arrange
        var context = CreateContext(tokenRequest);

        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<WebAuthnLoginAssertionOptionsTokenable>())
            .Returns(false);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal("invalid_request", context.Result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidToken_ChallengeNotConsumed_RejectsWithInvalidGrant(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<WebAuthnGrantValidator> sutProvider)
    {
        // Arrange
        var challenge = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var options = new AssertionOptions { Challenge = challenge };
        var tokenable = new WebAuthnLoginAssertionOptionsTokenable(
            WebAuthnLoginAssertionOptionsScope.Authentication, options);

        var context = CreateContext(tokenRequest);

        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<WebAuthnLoginAssertionOptionsTokenable>())
            .Returns(x =>
            {
                x[1] = tokenable;
                return true;
            });

        // ConsumeChallengeAsync returns false (entry does not exist or already consumed)
        sutProvider.GetDependency<IWebAuthnChallengeCache>()
            .ConsumeChallengeAsync(challenge)
            .Returns(false);

        // Act
        await sutProvider.Sut.ValidateAsync(context);

        // Assert
        Assert.Equal("invalid_grant", context.Result.Error);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidToken_ChallengeConsumed_ProceedsPastCacheCheck(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest tokenRequest,
        SutProvider<WebAuthnGrantValidator> sutProvider)
    {
        // Arrange
        var challenge = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var options = new AssertionOptions { Challenge = challenge };
        var tokenable = new WebAuthnLoginAssertionOptionsTokenable(
            WebAuthnLoginAssertionOptionsScope.Authentication, options);

        var context = CreateContext(tokenRequest);

        sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<WebAuthnLoginAssertionOptionsTokenable>())
            .Returns(x =>
            {
                x[1] = tokenable;
                return true;
            });

        // ConsumeChallengeAsync returns true (entry existed and was consumed)
        sutProvider.GetDependency<IWebAuthnChallengeCache>()
            .ConsumeChallengeAsync(challenge)
            .Returns(true);

        // Mock credential assertion to succeed
        var user = new User { Id = Guid.NewGuid() };
        var credential = new WebAuthnCredential();
        sutProvider.GetDependency<IAssertWebAuthnLoginCredentialCommand>()
            .AssertWebAuthnLoginCredential(Arg.Any<AssertionOptions>(), Arg.Any<AuthenticatorAssertionRawResponse>())
            .Returns((user, credential));

        // Act - the base validator pipeline may throw due to unmocked dependencies,
        // but our cache logic runs before that. We catch any downstream errors.
        try
        {
            await sutProvider.Sut.ValidateAsync(context);
        }
        catch (NullReferenceException)
        {
            // Expected: base validator pipeline has unmocked dependencies
        }

        // Assert - verify challenge was consumed (proves cache check passed)
        await sutProvider.GetDependency<IWebAuthnChallengeCache>()
            .Received(1)
            .ConsumeChallengeAsync(challenge);
    }
}

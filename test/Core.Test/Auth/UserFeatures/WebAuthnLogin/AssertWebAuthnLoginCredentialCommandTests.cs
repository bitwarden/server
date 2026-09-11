using System.Text;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class AssertWebAuthnLoginCredentialCommandTests
{
    [Theory, BitAutoData]
    internal async Task ChallengeNotCacheable_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge)
            .Returns(false);

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task InvalidUserHandle_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge).Returns(true);
        response = WithUserHandle(response, Encoding.UTF8.GetBytes("invalid-user-handle"));

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task UserNotFound_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge).Returns(true);
        response = WithUserHandle(response, user.Id.ToByteArray());
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).ReturnsNull();

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task NoMatchingCredentialExists_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge).Returns(true);
        response = WithUserHandle(response, user.Id.ToByteArray());
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { });

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task AssertionFails_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response, WebAuthnCredential credential)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge).Returns(true);
        var credentialId = Guid.NewGuid().ToByteArray();
        credential.CredentialId = CoreHelpers.Base64UrlEncode(credentialId);
        response = WithIdAndUserHandle(response, CoreHelpers.Base64UrlEncode(credentialId), user.Id.ToByteArray());
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { credential });
        sutProvider.GetDependency<IFido2>().MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<Fido2VerificationException>();

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task AssertionSucceeds_ReturnsUserAndCredential(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response, WebAuthnCredential credential)
    {
        // Arrange
        sutProvider.GetDependency<IWebAuthnChallengeCacheProvider>()
            .TryMarkChallengeAsUsedAsync(options.Challenge).Returns(true);
        var credentialId = Guid.NewGuid().ToByteArray();
        credential.CredentialId = CoreHelpers.Base64UrlEncode(credentialId);
        response = WithIdAndUserHandle(response, CoreHelpers.Base64UrlEncode(credentialId), user.Id.ToByteArray());
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { credential });
        sutProvider.GetDependency<IFido2>().MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyAssertionResult { CredentialId = credentialId, SignCount = 1 });

        // Act
        var result = await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        var (userResult, credentialResult) = result;
        Assert.Equal(user, userResult);
        Assert.Equal(credential, credentialResult);
    }

    // AuthenticatorAssertionRawResponse.Id and .Response.UserHandle are init-only in Fido2.AspNet v4,
    // so overriding them on an AutoFixture-generated instance requires rebuilding the object.
    private static AuthenticatorAssertionRawResponse WithUserHandle(AuthenticatorAssertionRawResponse response, byte[] userHandle)
        => WithIdAndUserHandle(response, response.Id, userHandle);

    private static AuthenticatorAssertionRawResponse WithIdAndUserHandle(AuthenticatorAssertionRawResponse response, string id, byte[] userHandle)
    {
        return new AuthenticatorAssertionRawResponse
        {
            Id = id,
            RawId = response.RawId,
            Type = response.Type,
            ClientExtensionResults = response.ClientExtensionResults,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                AuthenticatorData = response.Response.AuthenticatorData,
                Signature = response.Response.Signature,
                ClientDataJson = response.Response.ClientDataJson,
                UserHandle = userHandle,
            }
        };
    }
}

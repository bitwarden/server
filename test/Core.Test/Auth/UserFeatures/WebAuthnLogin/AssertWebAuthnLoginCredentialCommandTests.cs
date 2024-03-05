using System.Text;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
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
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class AssertWebAuthnLoginCredentialCommandTests
{
    [Theory, BitAutoData]
    internal async Task InvalidUserHandle_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        response.Response.UserHandle = Encoding.UTF8.GetBytes("invalid-user-handle");

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task UserNotFound_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response)
    {
        // Arrange
        response.Response.UserHandle = user.Id.ToByteArray();
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
        response.Response.UserHandle = user.Id.ToByteArray();
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { });

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task AssertionFails_ThrowsBadRequestException(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response, WebAuthnCredential credential, AssertionVerificationResult assertionResult)
    {
        // Arrange
        var credentialId = Guid.NewGuid().ToByteArray();
        credential.CredentialId = CoreHelpers.Base64UrlEncode(credentialId);
        response.Id = credentialId;
        response.Response.UserHandle = user.Id.ToByteArray();
        assertionResult.Status = "Not ok";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { credential });
        sutProvider.GetDependency<IFido2>().MakeAssertionAsync(response, options, Arg.Any<byte[]>(), Arg.Any<uint>(), Arg.Any<IsUserHandleOwnerOfCredentialIdAsync>())
            .Returns(assertionResult);

        // Act
        var result = async () => await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        await Assert.ThrowsAsync<BadRequestException>(result);
    }

    [Theory, BitAutoData]
    internal async Task AssertionSucceeds_ReturnsUserAndCredential(SutProvider<AssertWebAuthnLoginCredentialCommand> sutProvider, User user, AssertionOptions options, AuthenticatorAssertionRawResponse response, WebAuthnCredential credential, AssertionVerificationResult assertionResult)
    {
        // Arrange
        var credentialId = Guid.NewGuid().ToByteArray();
        credential.CredentialId = CoreHelpers.Base64UrlEncode(credentialId);
        response.Id = credentialId;
        response.Response.UserHandle = user.Id.ToByteArray();
        assertionResult.Status = "ok";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new WebAuthnCredential[] { credential });
        sutProvider.GetDependency<IFido2>().MakeAssertionAsync(response, options, Arg.Any<byte[]>(), Arg.Any<uint>(), Arg.Any<IsUserHandleOwnerOfCredentialIdAsync>())
            .Returns(assertionResult);

        // Act
        var result = await sutProvider.Sut.AssertWebAuthnLoginCredential(options, response);

        // Assert
        var (userResult, credentialResult) = result;
        Assert.Equal(user, userResult);
        Assert.Equal(credential, credentialResult);
    }
}

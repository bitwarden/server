using AutoFixture;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class CreateWebAuthnLoginCredentialCommandTests
{
    [Theory, BitAutoData]
    internal async Task ExceedsExistingCredentialsLimit_ReturnsFalse(SutProvider<CreateWebAuthnLoginCredentialCommand> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator)
    {
        // Arrange
        var existingCredentials = credentialGenerator.Take(CreateWebAuthnLoginCredentialCommand.MaxCredentialsPerUser).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);

        // Act
        var result = await sutProvider.Sut.CreateWebAuthnLoginCredentialAsync(user, "name", options, response, false, null, null, null);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().DidNotReceive().CreateAsync(Arg.Any<WebAuthnCredential>());
    }

    [Theory, BitAutoData]
    internal async Task DoesNotExceedExistingCredentialsLimit_CreatesCredential(SutProvider<CreateWebAuthnLoginCredentialCommand> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator)
    {
        // Arrange
        var existingCredentials = credentialGenerator.Take(CreateWebAuthnLoginCredentialCommand.MaxCredentialsPerUser - 1).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);
        sutProvider.GetDependency<IFido2>().MakeNewCredentialAsync(
            Arg.Any<MakeNewCredentialParams>(), Arg.Any<CancellationToken>()
        ).Returns(MakeCredentialResult());

        // Act
        var result = await sutProvider.Sut.CreateWebAuthnLoginCredentialAsync(user, "name", options, response, false, null, null, null);

        // Assert
        Assert.NotNull(result);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().Received().CreateAsync(Arg.Any<WebAuthnCredential>());
    }

    [Theory, BitAutoData]
    internal async Task MakeNewCredentialAsyncThrows_ReturnsNull(SutProvider<CreateWebAuthnLoginCredentialCommand> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator)
    {
        // Arrange
        var existingCredentials = credentialGenerator.Take(CreateWebAuthnLoginCredentialCommand.MaxCredentialsPerUser - 1).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);
        sutProvider.GetDependency<IFido2>().MakeNewCredentialAsync(
            Arg.Any<MakeNewCredentialParams>(), Arg.Any<CancellationToken>()
        ).ThrowsAsync<Fido2VerificationException>();

        // Act
        var result = await sutProvider.Sut.CreateWebAuthnLoginCredentialAsync(user, "name", options, response, false, null, null, null);

        // Assert
        Assert.Null(result);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().DidNotReceive().CreateAsync(Arg.Any<WebAuthnCredential>());
    }

    private RegisteredPublicKeyCredential MakeCredentialResult()
    {
        return new RegisteredPublicKeyCredential
        {
            AaGuid = new Guid(),
            SignCount = 0,
            Id = new Guid().ToByteArray(),
            AttestationFormat = "public-key",
            PublicKey = new byte[0],
            User = new Fido2User(),
        };
    }
}

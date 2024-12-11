using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class GetWebAuthnLoginCredentialCreateOptionsTests
{
    [Theory, BitAutoData]
    internal async Task NoExistingCredentials_ReturnsOptionsWithoutExcludedCredentials(
        SutProvider<GetWebAuthnLoginCredentialCreateOptionsCommand> sutProvider,
        User user
    )
    {
        // Arrange
        sutProvider
            .GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<WebAuthnCredential>());

        // Act
        var result = await sutProvider.Sut.GetWebAuthnLoginCredentialCreateOptionsAsync(user);

        // Assert
        sutProvider
            .GetDependency<IFido2>()
            .Received()
            .RequestNewCredential(
                Arg.Any<Fido2User>(),
                Arg.Is<List<PublicKeyCredentialDescriptor>>(list => list.Count == 0),
                Arg.Any<AuthenticatorSelection>(),
                Arg.Any<AttestationConveyancePreference>(),
                Arg.Any<AuthenticationExtensionsClientInputs>()
            );
    }

    [Theory, BitAutoData]
    internal async Task HasExistingCredential_ReturnsOptionsWithExcludedCredential(
        SutProvider<GetWebAuthnLoginCredentialCreateOptionsCommand> sutProvider,
        User user,
        WebAuthnCredential credential
    )
    {
        // Arrange
        sutProvider
            .GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<WebAuthnCredential> { credential });

        // Act
        var result = await sutProvider.Sut.GetWebAuthnLoginCredentialCreateOptionsAsync(user);

        // Assert
        sutProvider
            .GetDependency<IFido2>()
            .Received()
            .RequestNewCredential(
                Arg.Any<Fido2User>(),
                Arg.Is<List<PublicKeyCredentialDescriptor>>(list => list.Count == 1),
                Arg.Any<AuthenticatorSelection>(),
                Arg.Any<AttestationConveyancePreference>(),
                Arg.Any<AuthenticationExtensionsClientInputs>()
            );
    }
}

using AutoFixture;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using Xunit;
using static Fido2NetLib.Fido2;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class CreateWebAuthnLoginCredentialCommandTests
{
    [Theory, BitAutoData]
    internal async Task ExceedsExistingCredentialsLimit_ReturnsFalse(SutProvider<CreateWebAuthnLoginCredentialCommand> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator, bool userCanAccessPremium)
    {
        var webAuthNGlobalSettings = sutProvider.GetDependency<GlobalSettings>().WebAuthN = new GlobalSettings.WebAuthNSettings()
        {
            NonPremiumMaximumAllowedCredentials = 5,
            PremiumMaximumAllowedCredentials = 10,
        };
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(userCanAccessPremium);
        var maximumAllowedCredentialCount = userCanAccessPremium ? webAuthNGlobalSettings.PremiumMaximumAllowedCredentials : webAuthNGlobalSettings.NonPremiumMaximumAllowedCredentials;
        var existingCredentials = credentialGenerator.Take(maximumAllowedCredentialCount).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);

        // Act
        var result = await sutProvider.Sut.CreateWebAuthnLoginCredentialAsync(user, "name", options, response, false, null, null, null);

        // Assert
        Assert.False(result);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().DidNotReceive().CreateAsync(Arg.Any<WebAuthnCredential>());
    }

    [Theory, BitAutoData]
    internal async Task DoesNotExceedExistingCredentialsLimit_CreatesCredential(SutProvider<CreateWebAuthnLoginCredentialCommand> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator, bool userCanAccessPremium)
    {
        // Arrange
        var webAuthNGlobalSettings = sutProvider.GetDependency<GlobalSettings>().WebAuthN = new GlobalSettings.WebAuthNSettings()
        {
            NonPremiumMaximumAllowedCredentials = 5,
            PremiumMaximumAllowedCredentials = 10,
        };
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(userCanAccessPremium);
        var maximumAllowedCredentialCount = userCanAccessPremium ? webAuthNGlobalSettings.PremiumMaximumAllowedCredentials : webAuthNGlobalSettings.NonPremiumMaximumAllowedCredentials;
        var existingCredentials = credentialGenerator.Take(maximumAllowedCredentialCount - 1).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);
        sutProvider.GetDependency<IFido2>().MakeNewCredentialAsync(
            response, options, Arg.Any<IsCredentialIdUniqueToUserAsyncDelegate>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()
        ).Returns(MakeCredentialResult());

        // Act
        var result = await sutProvider.Sut.CreateWebAuthnLoginCredentialAsync(user, "name", options, response, false, null, null, null);

        // Assert
        Assert.True(result);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().Received().CreateAsync(Arg.Any<WebAuthnCredential>());
    }

    private CredentialMakeResult MakeCredentialResult()
    {
        return new CredentialMakeResult("ok", "", new AttestationVerificationSuccess
        {
            Aaguid = new Guid(),
            Counter = 0,
            CredentialId = new Guid().ToByteArray(),
            CredType = "public-key",
            PublicKey = new byte[0],
            Status = "ok",
            User = new Fido2User(),
        });
    }
}

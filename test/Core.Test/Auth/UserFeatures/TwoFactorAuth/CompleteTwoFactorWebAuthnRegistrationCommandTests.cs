using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class CompleteTwoFactorWebAuthnRegistrationCommandTests
{
    /// <summary>
    /// The "Start" command will have set the in-process credential registration request to "pending" status.
    /// The purpose of Complete is to consume and enshrine this pending credential.
    /// </summary>
    private static void SetupWebAuthnProviderWithPending(User user, int credentialCount)
    {
        var providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        var metadata = new Dictionary<string, object>();

        // Add existing credentials
        for (var i = 1; i <= credentialCount; i++)
        {
            metadata[$"Key{i}"] = new TwoFactorProvider.WebAuthnData
            {
                Name = $"Key {i}",
                Descriptor = new PublicKeyCredentialDescriptor([(byte)i]),
                PublicKey = [(byte)i],
                UserHandle = [(byte)i],
                SignatureCounter = 0,
                CredType = "public-key",
                RegDate = DateTime.UtcNow,
                AaGuid = Guid.NewGuid()
            };
        }

        // Add pending registration
        var pendingOptions = new CredentialCreateOptions
        {
            Challenge = [1, 2, 3],
            Rp = new PublicKeyCredentialRpEntity("example.com", "example.com", ""),
            User = new Fido2User
            {
                Id = user.Id.ToByteArray(),
                Name = user.Email ?? "test@example.com",
                DisplayName = user.Name ?? "Test User"
            },
            PubKeyCredParams = []
        };
        metadata["pending"] = pendingOptions.ToJson();

        providers[TwoFactorProviderType.WebAuthn] = new TwoFactorProvider { Enabled = true, MetaData = metadata };

        user.SetTwoFactorProviders(providers);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task CompleteWebAuthRegistrationAsync_BelowLimit_Succeeds(bool hasPremium,
        SutProvider<CompleteTwoFactorWebAuthnRegistrationCommand> sutProvider, User user,
        AuthenticatorAttestationRawResponse deviceResponse)
    {
        // Arrange - User should have 1 available credential, determined by maximum allowed for tested Premium status.
        var maximumAllowedCredentialsGlobalSetting = new Core.Settings.GlobalSettings.WebAuthnSettings
        {
            PremiumMaximumAllowedCredentials = 10,
            NonPremiumMaximumAllowedCredentials = 5
        };

        sutProvider.GetDependency<IGlobalSettings>().WebAuthn = maximumAllowedCredentialsGlobalSetting;

        user.Premium = hasPremium;
        user.Id = Guid.NewGuid();
        user.Email = "test@example.com";

        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(user.Id).Returns(hasPremium);

        SetupWebAuthnProviderWithPending(user,
            credentialCount: hasPremium
                ? maximumAllowedCredentialsGlobalSetting.PremiumMaximumAllowedCredentials - 1
                : maximumAllowedCredentialsGlobalSetting.NonPremiumMaximumAllowedCredentials - 1);

        var mockFido2 = sutProvider.GetDependency<IFido2>();
        mockFido2.MakeNewCredentialAsync(
                Arg.Any<AuthenticatorAttestationRawResponse>(),
                Arg.Any<CredentialCreateOptions>(),
                Arg.Any<IsCredentialIdUniqueToUserAsyncDelegate>())
            .Returns(new Fido2.CredentialMakeResult("ok", "",
                new AttestationVerificationSuccess
                {
                    Aaguid = Guid.NewGuid(),
                    Counter = 0,
                    CredentialId = [1, 2, 3],
                    CredType = "public-key",
                    PublicKey = [4, 5, 6],
                    Status = "ok",
                    User = new Fido2User
                    {
                        Id = user.Id.ToByteArray(),
                        Name = user.Email ?? "test@example.com",
                        DisplayName = user.Name ?? "Test User"
                    }
                }));

        // Act
        var result =
            await sutProvider.Sut.CompleteTwoFactorWebAuthnRegistrationAsync(user, 5, "NewKey", deviceResponse);

        // Assert
        // Note that, contrary to the "Start" command, "Complete" does not suppress logging for the update providers invocation.
        Assert.True(result);
        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task CompleteWebAuthRegistrationAsync_ExceedsLimit_ThrowsBadRequestException(bool hasPremium,
        SutProvider<CompleteTwoFactorWebAuthnRegistrationCommand> sutProvider, User user,
        AuthenticatorAttestationRawResponse deviceResponse)
    {
        // Arrange - time-of-check/time-of-use scenario: user now has 10 credentials (at limit)
        var maximumAllowedCredentialsGlobalSetting = new Core.Settings.GlobalSettings.WebAuthnSettings
        {
            PremiumMaximumAllowedCredentials = 10,
            NonPremiumMaximumAllowedCredentials = 5
        };

        sutProvider.GetDependency<IGlobalSettings>().WebAuthn = maximumAllowedCredentialsGlobalSetting;

        user.Premium = hasPremium;
        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(user.Id).Returns(hasPremium);


        SetupWebAuthnProviderWithPending(user,
            credentialCount: hasPremium
                ? maximumAllowedCredentialsGlobalSetting.PremiumMaximumAllowedCredentials
                : maximumAllowedCredentialsGlobalSetting.NonPremiumMaximumAllowedCredentials);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CompleteTwoFactorWebAuthnRegistrationAsync(user, 11, "NewKey", deviceResponse));

        Assert.Equal("Maximum allowed WebAuthn credential count exceeded.", exception.Message);
    }
}

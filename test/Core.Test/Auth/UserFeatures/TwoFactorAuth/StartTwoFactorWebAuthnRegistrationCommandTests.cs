using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class StartTwoFactorWebAuthnRegistrationCommandTests
{
    private static void SetupWebAuthnProvider(User user, int credentialCount)
    {
        var providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        var metadata = new Dictionary<string, object>();

        // Add credentials as Key1, Key2, Key3, etc.
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

        providers[TwoFactorProviderType.WebAuthn] = new TwoFactorProvider { Enabled = true, MetaData = metadata };

        user.SetTwoFactorProviders(providers);
    }

    /// <summary>
    /// This is different from <see cref="SetupWebAuthnProvider"/> since this assigns the 
    /// raw serialized <see cref="User.TwoFactorProviders"/> string. SetTwoFactorProviders caches the typed
    /// WebAuthnData objects in-memory, so GetTwoFactorProviders() returns them directly and never deserializes the
    /// JSON. Assigning the string directly forces a real deserialization on next access, producing Newtonsoft
    /// JObject-backed metadata values just like a genuine load from the database.
    /// </summary>
    private static void SetupWebAuthnProviderViaSerializedMetadata(User user, int credentialCount)
    {
        var providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        var metadata = new Dictionary<string, object>();

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

        providers[TwoFactorProviderType.WebAuthn] = new TwoFactorProvider { Enabled = true, MetaData = metadata };

        user.TwoFactorProviders = JsonHelpers.LegacySerialize(providers, JsonHelpers.LegacyEnumKeyResolver);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task StartWebAuthnRegistrationAsync_BelowLimit_Succeeds(
        bool hasPremium, SutProvider<StartTwoFactorWebAuthnRegistrationCommand> sutProvider, User user)
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

        SetupWebAuthnProvider(user,
            credentialCount: hasPremium
                ? maximumAllowedCredentialsGlobalSetting.PremiumMaximumAllowedCredentials - 1
                : maximumAllowedCredentialsGlobalSetting.NonPremiumMaximumAllowedCredentials - 1);

        var mockFido2 = sutProvider.GetDependency<IFido2>();
        mockFido2.RequestNewCredential(Arg.Any<RequestNewCredentialParams>())
            .Returns(new CredentialCreateOptions
            {
                Challenge = [1, 2, 3],
                Rp = new PublicKeyCredentialRpEntity("example.com", "example.com", ""),
                User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.Name },
                PubKeyCredParams = []
            });

        // Act
        var result = await sutProvider.Sut.StartTwoFactorWebAuthnRegistrationAsync(user);

        // Assert
        Assert.NotNull(result);
        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, false);
    }

    /// <summary>
    /// "Start" provides the first half of a two-part process for registering a new WebAuthn 2FA credential.
    /// To provide the best (most aggressive) UX possible, "Start" performs boundary validation of the ability to engage
    /// in this flow based on current number of configured credentials. If the user is out of available credential slots,
    /// Start should throw a BadRequestException for the client to handle.
    /// </summary>
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task StartWebAuthnRegistrationAsync_ExceedsLimit_ThrowsBadRequestException(
        bool hasPremium, SutProvider<StartTwoFactorWebAuthnRegistrationCommand> sutProvider, User user)
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

        SetupWebAuthnProvider(user,
            credentialCount: hasPremium
                ? maximumAllowedCredentialsGlobalSetting.PremiumMaximumAllowedCredentials
                : maximumAllowedCredentialsGlobalSetting.NonPremiumMaximumAllowedCredentials);

        var mockFido2 = sutProvider.GetDependency<IFido2>();
        mockFido2.RequestNewCredential(Arg.Any<RequestNewCredentialParams>())
            .Returns(new CredentialCreateOptions
            {
                Challenge = [1, 2, 3],
                Rp = new PublicKeyCredentialRpEntity("example.com", "example.com", ""),
                User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.Name },
                PubKeyCredParams = []
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.StartTwoFactorWebAuthnRegistrationAsync(user));
        Assert.Equal("Maximum allowed WebAuthn credential count exceeded.", exception.Message);
    }

    /// <summary>
    /// This test helps test the deserialization of the JObject contained in the dynamic type when trying
    /// to parse the TwoFactor MetaData.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task StartWebAuthnRegistrationAsync_ExistingCredentials_AreExcluded(
        SutProvider<StartTwoFactorWebAuthnRegistrationCommand> sutProvider, User user)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>().WebAuthn = new Core.Settings.GlobalSettings.WebAuthnSettings
        {
            PremiumMaximumAllowedCredentials = 10,
            NonPremiumMaximumAllowedCredentials = 5
        };

        user.Premium = false;
        user.Id = Guid.NewGuid();
        user.Email = "test@example.com";

        sutProvider.GetDependency<IHasPremiumAccessQuery>().HasPremiumAccessAsync(user.Id).Returns(false);

        SetupWebAuthnProviderViaSerializedMetadata(user, credentialCount: 2);

        RequestNewCredentialParams capturedParams = null;
        var mockFido2 = sutProvider.GetDependency<IFido2>();
        mockFido2.RequestNewCredential(Arg.Do<RequestNewCredentialParams>(p => capturedParams = p))
            .Returns(new CredentialCreateOptions
            {
                Challenge = [1, 2, 3],
                Rp = new PublicKeyCredentialRpEntity("example.com", "example.com", ""),
                User = new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.Name },
                PubKeyCredParams = []
            });

        // Act
        await sutProvider.Sut.StartTwoFactorWebAuthnRegistrationAsync(user);

        // Assert
        Assert.NotNull(capturedParams);
        Assert.Equal(2, capturedParams.ExcludeCredentials.Count);
        Assert.Contains(capturedParams.ExcludeCredentials, c => c.Id.SequenceEqual(new byte[] { 1 }));
        Assert.Contains(capturedParams.ExcludeCredentials, c => c.Id.SequenceEqual(new byte[] { 2 }));
    }
}

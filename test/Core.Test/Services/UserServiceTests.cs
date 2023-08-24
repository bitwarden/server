using System.Text.Json;
using AutoFixture;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Fido2NetLib;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class UserServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveUserAsync_SetsNameToNull_WhenNameIsEmpty(SutProvider<UserService> sutProvider, User user)
    {
        user.Name = string.Empty;
        await sutProvider.Sut.SaveUserAsync(user);
        Assert.Null(user.Name);
    }

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_Success(SutProvider<UserService> sutProvider,
        User user, UserLicense userLicense)
    {
        using var tempDir = new TempDirectory();

        var now = DateTime.UtcNow;
        userLicense.Issued = now.AddDays(-10);
        userLicense.Expires = now.AddDays(10);
        userLicense.Version = 1;
        userLicense.Premium = true;

        user.EmailVerified = true;
        user.Email = userLicense.Email;

        sutProvider.GetDependency<Settings.IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<Settings.IGlobalSettings>().LicenseDirectory = tempDir.Directory;
        sutProvider.GetDependency<ILicensingService>()
            .VerifyLicense(userLicense)
            .Returns(true);

        await sutProvider.Sut.UpdateLicenseAsync(user, userLicense);

        var filePath = Path.Combine(tempDir.Directory, "user", $"{user.Id}.json");
        Assert.True(File.Exists(filePath));
        var document = JsonDocument.Parse(File.OpenRead(filePath));
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        // Sort of a lazy way to test that it is indented but not sure of a better way
        Assert.Contains('\n', root.GetRawText());
        AssertHelper.AssertJsonProperty(root, "LicenseKey", JsonValueKind.String);
        AssertHelper.AssertJsonProperty(root, "Id", JsonValueKind.String);
        AssertHelper.AssertJsonProperty(root, "Premium", JsonValueKind.True);
        var versionProp = AssertHelper.AssertJsonProperty(root, "Version", JsonValueKind.Number);
        Assert.Equal(1, versionProp.GetInt32());
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_Success(SutProvider<UserService> sutProvider, User user)
    {
        var email = user.Email.ToLowerInvariant();
        var token = "thisisatokentocompare";

        var userTwoFactorTokenProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        userTwoFactorTokenProvider
            .CanGenerateTwoFactorTokenAsync(Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(true));
        userTwoFactorTokenProvider
            .GenerateAsync("2faEmail:" + email, Arg.Any<UserManager<User>>(), user)
            .Returns(Task.FromResult(token));

        sutProvider.Sut.RegisterTokenProvider("Email", userTwoFactorTokenProvider);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = email },
                Enabled = true
            }
        });
        await sutProvider.Sut.SendTwoFactorEmailAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendTwoFactorEmailAsync(email, token);
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderOnUser(SutProvider<UserService> sutProvider, User user)
    {
        user.TwoFactorProviders = null;

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderMetadataOnUser(SutProvider<UserService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = null,
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task SendTwoFactorEmailAsync_ExceptionBecauseNoProviderEmailMetadataOnUser(SutProvider<UserService> sutProvider, User user)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["qweqwe"] = user.Email.ToLowerInvariant() },
                Enabled = true
            }
        });

        await Assert.ThrowsAsync<ArgumentNullException>("No email.", () => sutProvider.Sut.SendTwoFactorEmailAsync(user));
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganization_Returns_False_If_No_Orgs(SutProvider<UserService> sutProvider, User user)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>());
        Assert.False(await sutProvider.Sut.HasPremiumFromOrganization(user));

    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    public async Task HasPremiumFromOrganization_Returns_False_If_Org_Not_Eligible(bool orgEnabled, bool orgUsersGetPremium, SutProvider<UserService> sutProvider, User user, OrganizationUser orgUser, Organization organization)
    {
        orgUser.OrganizationId = organization.Id;
        organization.Enabled = orgEnabled;
        organization.UsersGetPremium = orgUsersGetPremium;
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>() { { organization.Id, new OrganizationAbility(organization) } };

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>() { orgUser });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);

        Assert.False(await sutProvider.Sut.HasPremiumFromOrganization(user));
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganization_Returns_True_If_Org_Eligible(SutProvider<UserService> sutProvider, User user, OrganizationUser orgUser, Organization organization)
    {
        orgUser.OrganizationId = organization.Id;
        organization.Enabled = true;
        organization.UsersGetPremium = true;
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>() { { organization.Id, new OrganizationAbility(organization) } };

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>() { orgUser });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);

        Assert.True(await sutProvider.Sut.HasPremiumFromOrganization(user));
    }

    [Theory, BitAutoData]
    public async void CompleteWebAuthLoginRegistrationAsync_ExceedsExistingCredentialsLimit_ReturnsFalse(SutProvider<UserService> sutProvider, User user, CredentialCreateOptions options, AuthenticatorAttestationRawResponse response, Generator<WebAuthnCredential> credentialGenerator)
    {
        // Arrange
        var existingCredentials = credentialGenerator.Take(5).ToList();
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(existingCredentials);

        // Act
        var result = await sutProvider.Sut.CompleteWebAuthLoginRegistrationAsync(user, "name", null, null, null, false, options, response);

        // Assert
        Assert.False(result);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().DidNotReceive();
    }

    [Flags]
    public enum ShouldCheck
    {
        Password = 0x1,
        OTP = 0x2,
    }

    [Theory]
    // A user who has a password, and the password is valid should only check for that password
    [BitAutoData(true, "test_password", true, ShouldCheck.Password)]
    // A user who does not have a password, should only check if the OTP is valid
    [BitAutoData(false, "otp_token", true, ShouldCheck.OTP)]
    // A user who has a password but supplied a OTP, it will check password first and then try OTP
    [BitAutoData(true, "otp_token", true, ShouldCheck.Password | ShouldCheck.OTP)]
    // A user who does not have a password and supplied an invalid OTP token, should only check OTP and return invalid
    [BitAutoData(false, "bad_otp_token", false, ShouldCheck.OTP)]
    // A user who does have a password but they supply a bad one, we will check both but it will still be invalid
    [BitAutoData(true, "bad_test_password", false, ShouldCheck.Password | ShouldCheck.OTP)]
    public async Task VerifySecretAsync_Works(
        bool shouldHavePassword, string secret, bool expectedIsVerified, ShouldCheck shouldCheck, // inline theory data
        SutProvider<UserService> sutProvider, User user) // AutoFixture injected data
    {
        // Arrange
        var tokenProvider = SetupFakeTokenProvider(sutProvider, user);
        SetupUserAndDevice(user, shouldHavePassword);

        // Setup the fake password verification
        var substitutedUserPasswordStore = Substitute.For<IUserPasswordStore<User>>();
        substitutedUserPasswordStore
            .GetPasswordHashAsync(user, Arg.Any<CancellationToken>())
            .Returns((ci) =>
            {
                return Task.FromResult("hashed_test_password");
            });

        sutProvider.SetDependency<IUserStore<User>>(substitutedUserPasswordStore, "store");

        sutProvider.GetDependency<IPasswordHasher<User>>("passwordHasher")
            .VerifyHashedPassword(user, "hashed_test_password", "test_password")
            .Returns((ci) =>
            {
                return PasswordVerificationResult.Success;
            });

        // HACK: SutProvider is being weird about not injecting the IPasswordHasher that I configured
        var sut = new UserService(
            sutProvider.GetDependency<IUserRepository>(),
            sutProvider.GetDependency<ICipherRepository>(),
            sutProvider.GetDependency<IOrganizationUserRepository>(),
            sutProvider.GetDependency<IOrganizationRepository>(),
            sutProvider.GetDependency<IMailService>(),
            sutProvider.GetDependency<IPushNotificationService>(),
            sutProvider.GetDependency<IUserStore<User>>(),
            sutProvider.GetDependency<IOptions<IdentityOptions>>(),
            sutProvider.GetDependency<IPasswordHasher<User>>(),
            sutProvider.GetDependency<IEnumerable<IUserValidator<User>>>(),
            sutProvider.GetDependency<IEnumerable<IPasswordValidator<User>>>(),
            sutProvider.GetDependency<ILookupNormalizer>(),
            sutProvider.GetDependency<IdentityErrorDescriber>(),
            sutProvider.GetDependency<IServiceProvider>(),
            sutProvider.GetDependency<ILogger<UserManager<User>>>(),
            sutProvider.GetDependency<ILicensingService>(),
            sutProvider.GetDependency<IEventService>(),
            sutProvider.GetDependency<IApplicationCacheService>(),
            sutProvider.GetDependency<IDataProtectionProvider>(),
            sutProvider.GetDependency<IPaymentService>(),
            sutProvider.GetDependency<IPolicyRepository>(),
            sutProvider.GetDependency<IPolicyService>(),
            sutProvider.GetDependency<IReferenceEventService>(),
            sutProvider.GetDependency<IFido2>(),
            sutProvider.GetDependency<ICurrentContext>(),
            sutProvider.GetDependency<IGlobalSettings>(),
            sutProvider.GetDependency<IOrganizationService>(),
            sutProvider.GetDependency<IProviderUserRepository>(),
            sutProvider.GetDependency<IStripeSyncService>(),
            sutProvider.GetDependency<IWebAuthnCredentialRepository>(),
            sutProvider.GetDependency<IDataProtectorTokenFactory<WebAuthnLoginTokenable>>()
            );

        var actualIsVerified = await sut.VerifySecretAsync(user, secret);

        Assert.Equal(expectedIsVerified, actualIsVerified);

        await tokenProvider
            .Received(shouldCheck.HasFlag(ShouldCheck.OTP) ? 1 : 0)
            .ValidateAsync(Arg.Any<string>(), secret, Arg.Any<UserManager<User>>(), user);

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(shouldCheck.HasFlag(ShouldCheck.Password) ? 1 : 0)
            .VerifyHashedPassword(user, "hashed_test_password", secret);
    }

    private static void SetupUserAndDevice(User user,
        bool shouldHavePassword)
    {
        if (shouldHavePassword)
        {
            user.MasterPassword = "test_password";
        }
        else
        {
            user.MasterPassword = null;
        }
    }

    private static IUserTwoFactorTokenProvider<User> SetupFakeTokenProvider(SutProvider<UserService> sutProvider, User user)
    {
        var fakeUserTwoFactorProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();

        fakeUserTwoFactorProvider
            .GenerateAsync(Arg.Any<string>(), Arg.Any<UserManager<User>>(), user)
            .Returns("OTP_TOKEN");

        fakeUserTwoFactorProvider
            .ValidateAsync(Arg.Any<string>(), Arg.Is<string>(s => s != "otp_token"), Arg.Any<UserManager<User>>(), user)
            .Returns(false);

        fakeUserTwoFactorProvider
            .ValidateAsync(Arg.Any<string>(), "otp_token", Arg.Any<UserManager<User>>(), user)
            .Returns(true);

        sutProvider.GetDependency<IOptions<IdentityOptions>>()
            .Value.Returns(new IdentityOptions
            {
                Tokens = new TokenOptions
                {
                    ProviderMap = new Dictionary<string, TokenProviderDescriptor>()
                    {
                        ["Email"] = new TokenProviderDescriptor(typeof(IUserTwoFactorTokenProvider<User>))
                        {
                            ProviderInstance = fakeUserTwoFactorProvider,
                        }
                    }
                }
            });

        // The above arranging of dependencies is used in the constructor of UserManager
        // ref: https://github.com/dotnet/aspnetcore/blob/bfeb3bf9005c36b081d1e48725531ee0e15a9dfb/src/Identity/Extensions.Core/src/UserManager.cs#L103-L120
        // since the constructor of the Sut has ran already (when injected) I need to recreate it to get it to run again
        sutProvider.Create();

        return fakeUserTwoFactorProvider;
    }
}

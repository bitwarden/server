#nullable enable

using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Constants;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.RegistrationTestHelpers;

namespace Bit.Identity.IntegrationTest.Registration;

/// <summary>
/// The salt that a client sends at registration finish is stored as sent and is returned to every client
/// that derives the master key. A salt that contains '@' is an email-derived salt and must equal the
/// trimmed, lowercased email. A salt without '@' is accepted as sent.
/// </summary>
public class RegisterFinishMasterPasswordSaltTests
{
    private const string _invalidSaltMessage = "Invalid master password salt.";
    private const string _type7EncryptedString = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _type7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterFinish_EmailSaltWithTypedCase_ReturnsBadRequestAndCreatesNoUser(bool v2AccountKeys)
    {
        var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt: typedEmail, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, typedEmail);
    }

    [Fact]
    public async Task RegisterFinish_EmailSaltFromAnotherEmail_ReturnsBadRequestAndCreatesNoUser()
    {
        var factory = new IdentityApplicationFactory();
        var email = $"salt.test+{Guid.NewGuid():N}@example.com";
        var request = BuildRequest(email, salt: $"other+{Guid.NewGuid():N}@example.com", v2AccountKeys: false);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, email, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, email);
    }

    /// <summary>
    /// The salt rule runs before the registration token is checked, so placeholder tokens show that every
    /// token type gets the same rejection.
    /// </summary>
    [Theory]
    [InlineData(RegisterFinishTokenType.OrganizationInvite)]
    [InlineData(RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan)]
    [InlineData(RegisterFinishTokenType.EmergencyAccessInvite)]
    [InlineData(RegisterFinishTokenType.ProviderInvite)]
    [InlineData(RegisterFinishTokenType.SalesAssisted)]
    public async Task RegisterFinish_EmailSaltWithTypedCase_AnyTokenType_ReturnsBadRequestAndCreatesNoUser(
        RegisterFinishTokenType tokenType)
    {
        var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt: typedEmail, v2AccountKeys: false);
        SetPlaceholderToken(request, tokenType);
        Assert.Equal(tokenType, request.GetTokenType());

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, typedEmail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterFinish_EmailSaltTrimmedAndLowercased_StoresSaltAndReturnsItAtLogin(bool v2AccountKeys)
    {
        var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var salt = typedEmail.Trim().ToLowerInvariant();
        var request = BuildRequest(typedEmail, salt, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertSaltStoredAndReturnedAsync(factory, typedEmail, salt, v2AccountKeys);
    }

    [Theory]
    [InlineData("dGhpcyBpcyBhIHNhbHQgdmFsdWUgdGhhdCBpcyBub3QgYW4gZW1haWw=", false)]
    [InlineData("dGhpcyBpcyBhIHNhbHQgdmFsdWUgdGhhdCBpcyBub3QgYW4gZW1haWw=", true)]
    [InlineData("a3f1c2d4e5b60718293a4b5c6d7e8f90a3f1c2d4e5b60718293a4b5c6d7e8f90", false)]
    public async Task RegisterFinish_SaltWithoutAtSign_StoresSaltAsSentAndReturnsItAtLogin(string salt, bool v2AccountKeys)
    {
        var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertSaltStoredAndReturnedAsync(factory, typedEmail, salt, v2AccountKeys);
    }

    [Fact]
    public async Task RegisterFinish_LegacyShapeWithoutSalt_SucceedsAndStoresNoSalt()
    {
        var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = new RegisterFinishRequestModel
        {
            Email = typedEmail,
            EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test"),
            MasterPasswordHash = IdentityApplicationFactory.DefaultUserPasswordHash,
            UserSymmetricKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserAsymmetricKeys = new KeysRequestModel
            {
                PublicKey = TestEncryptionConstants.PublicKey,
                EncryptedPrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            },
        };

        var context = await factory.PostRegisterFinishAsync(request);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var user = await factory.GetDatabaseContext().Users
            .SingleAsync(u => u.Email.ToLower() == typedEmail.ToLower());
        Assert.Null(user.MasterPasswordSalt);
    }

    /// <summary>
    /// Gives an email with uppercase letters, as a user can type it on a mobile keyboard.
    /// </summary>
    private static string NewTypedEmail() => $"Salt.Test+{Guid.NewGuid():N}@Example.com";

    private static RegisterFinishRequestModel BuildRequest(string email, string salt, bool v2AccountKeys)
    {
        var kdf = new KdfRequestModel
        {
            KdfType = KdfType.PBKDF2_SHA256,
            Iterations = KdfConstants.PBKDF2_ITERATIONS.Default,
        };

        return new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
                Salt = salt,
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = IdentityApplicationFactory.DefaultUserPasswordHash,
                Salt = salt,
            },
            UserAsymmetricKeys = v2AccountKeys
                ? null
                : new KeysRequestModel
                {
                    PublicKey = TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
                },
            AccountKeys = v2AccountKeys ? BuildV2AccountKeys() : null,
        };
    }

    private static AccountKeysRequestModel BuildV2AccountKeys() => new()
    {
        AccountPublicKey = TestEncryptionConstants.PublicKey,
        UserKeyEncryptedAccountPrivateKey = _type7EncryptedString,
        PublicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairRequestModel
        {
            PublicKey = TestEncryptionConstants.PublicKey,
            WrappedPrivateKey = _type7EncryptedString,
            SignedPublicKey = "signed-public-key",
        },
        SignatureKeyPair = new SignatureKeyPairRequestModel
        {
            SignatureAlgorithm = "ed25519",
            WrappedSigningKey = _type7WrappedSigningKey,
            VerifyingKey = TestEncryptionConstants.V2VerifyingKey,
        },
        SecurityState = new SecurityStateModel
        {
            SecurityState = "v2",
            SecurityVersion = 2,
        },
    };

    private static void SetPlaceholderToken(RegisterFinishRequestModel request, RegisterFinishTokenType tokenType)
    {
        switch (tokenType)
        {
            case RegisterFinishTokenType.OrganizationInvite:
                request.OrgInviteToken = "placeholder-token";
                request.OrganizationUserId = Guid.NewGuid();
                break;
            case RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan:
                request.OrgSponsoredFreeFamilyPlanToken = "placeholder-token";
                break;
            case RegisterFinishTokenType.EmergencyAccessInvite:
                request.AcceptEmergencyAccessInviteToken = "placeholder-token";
                request.AcceptEmergencyAccessId = Guid.NewGuid();
                break;
            case RegisterFinishTokenType.ProviderInvite:
                request.ProviderInviteToken = "placeholder-token";
                request.ProviderUserId = Guid.NewGuid();
                break;
            case RegisterFinishTokenType.SalesAssisted:
                request.SalesAssistedToken = "placeholder-token";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tokenType), tokenType, null);
        }
    }

    private static async Task AssertInvalidSaltAndNoUserAsync(
        IdentityApplicationFactory factory, HttpContext context, string email)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var reader = new StreamReader(context.Response.Body);
        Assert.Contains(_invalidSaltMessage, await reader.ReadToEndAsync());
        Assert.False(await factory.GetDatabaseContext().Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower()));
    }

    private static async Task AssertSaltStoredAndReturnedAsync(
        IdentityApplicationFactory factory, string email, string expectedSalt, bool v2AccountKeys)
    {
        var user = await factory.GetDatabaseContext().Users
            .SingleAsync(u => u.Email.ToLower() == email.ToLower());
        Assert.Equal(expectedSalt, user.MasterPasswordSalt);
        Assert.Equal(v2AccountKeys, user.SecurityVersion is not null);

        // The test database compares emails case-sensitively, so prelogin and login use the stored email.
        var preloginContext = await factory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = user.Email });
        Assert.Equal(StatusCodes.Status200OK, preloginContext.Response.StatusCode);
        using var preloginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(preloginContext);
        Assert.Equal(expectedSalt, preloginBody.RootElement.GetProperty("salt").GetString());

        var loginContext = await factory.ContextFromPasswordAsync(user.Email, IdentityApplicationFactory.DefaultUserPasswordHash);
        Assert.Equal(StatusCodes.Status200OK, loginContext.Response.StatusCode);
        using var loginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(loginContext);
        var masterPasswordUnlock = loginBody.RootElement
            .GetProperty("UserDecryptionOptions")
            .GetProperty("MasterPasswordUnlock");
        Assert.Equal(expectedSalt, masterPasswordUnlock.GetProperty("Salt").GetString());
    }
}

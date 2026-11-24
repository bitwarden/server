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
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.Registration;

/// <summary>
/// Integration tests for the master password salt that a client sends at registration finish. The server stores the
/// salt as sent and returns it to every client that derives the master key. A salt that contains '@' is an
/// email-derived salt and must equal the trimmed, lowercased email. A salt without '@' is accepted as sent.
/// </summary>
public class RegisterFinishMasterPasswordSaltTests
{
    private const string _type7EncryptedString = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _type7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";

    /// <summary>
    /// An email salt that keeps the case that the user typed is rejected, and no account is created. Clients derive
    /// the master key from the lowercased email, so they could not reproduce this salt.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterFinish_EmailSaltWithTypedCase_IsRejected(bool v2AccountKeys)
    {
        using var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt: typedEmail, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, typedEmail);
    }

    /// <summary>
    /// An email salt from a different email is rejected, and no account is created.
    /// </summary>
    [Fact]
    public async Task RegisterFinish_EmailSaltFromAnotherEmail_IsRejected()
    {
        using var factory = new IdentityApplicationFactory();
        var email = $"salt.test+{Guid.NewGuid():N}@example.com";
        var request = BuildRequest(email, salt: $"other+{Guid.NewGuid():N}@example.com", v2AccountKeys: false);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, email, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, email);
    }

    /// <summary>
    /// The salt rule applies to every registration token type, not only to email verification.
    /// </summary>
    [Theory]
    [InlineData(RegisterFinishTokenType.OrganizationInvite)]
    [InlineData(RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan)]
    [InlineData(RegisterFinishTokenType.EmergencyAccessInvite)]
    [InlineData(RegisterFinishTokenType.ProviderInvite)]
    [InlineData(RegisterFinishTokenType.SalesAssisted)]
    public async Task RegisterFinish_EmailSaltWithTypedCase_IsRejectedForEveryTokenType(RegisterFinishTokenType tokenType)
    {
        using var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt: typedEmail, v2AccountKeys: false);
        // The salt rule runs before the token is checked, so a placeholder token is enough to select the type.
        SetPlaceholderToken(request, tokenType);
        Assert.Equal(tokenType, request.GetTokenType());

        var context = await factory.PostRegisterFinishAsync(request);

        await AssertInvalidSaltAndNoUserAsync(factory, context, typedEmail);
    }

    /// <summary>
    /// An email salt that equals the trimmed, lowercased email is stored as sent. Prelogin and the password login
    /// return it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterFinish_EmailSaltTrimmedAndLowercased_IsStoredAndReturned(bool v2AccountKeys)
    {
        using var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var salt = typedEmail.Trim().ToLowerInvariant();
        var request = BuildRequest(typedEmail, salt, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertSaltStoredAndReturnedAsync(factory, typedEmail, salt, v2AccountKeys);
    }

    /// <summary>
    /// A salt without '@' is not email-derived, such as a base64 or hex value. It is stored as sent, with no
    /// comparison to the email. Prelogin and the password login return it.
    /// </summary>
    [Theory]
    [InlineData("dGhpcyBpcyBhIHNhbHQgdmFsdWUgdGhhdCBpcyBub3QgYW4gZW1haWw=", false)]
    [InlineData("dGhpcyBpcyBhIHNhbHQgdmFsdWUgdGhhdCBpcyBub3QgYW4gZW1haWw=", true)]
    [InlineData("a3f1c2d4e5b60718293a4b5c6d7e8f90a3f1c2d4e5b60718293a4b5c6d7e8f90", false)]
    public async Task RegisterFinish_SaltWithoutAtSign_IsStoredAsSentAndReturned(string salt, bool v2AccountKeys)
    {
        using var factory = new IdentityApplicationFactory();
        var typedEmail = NewTypedEmail();
        var request = BuildRequest(typedEmail, salt, v2AccountKeys);
        request.EmailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(factory, typedEmail, "Salt Test");

        var context = await factory.PostRegisterFinishAsync(request);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertSaltStoredAndReturnedAsync(factory, typedEmail, salt, v2AccountKeys);
    }

    /// <summary>
    /// A registration request in the older format sends no salt. It still succeeds, and no salt is stored.
    /// </summary>
    [Fact]
    public async Task RegisterFinish_LegacyShapeWithoutSalt_StoresNoSalt()
    {
        using var factory = new IdentityApplicationFactory();
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

    /// <summary>
    /// Asserts a 400 response whose only validation error is the invalid salt, and that no account exists.
    /// </summary>
    private static async Task AssertInvalidSaltAndNoUserAsync(
        IdentityApplicationFactory factory, HttpContext context, string email)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var validationErrors = AssertHelper.AssertJsonProperty(body.RootElement, "validationErrors", JsonValueKind.Object);
        var error = Assert.Single(validationErrors.EnumerateObject());
        Assert.Equal(nameof(MasterPasswordUnlockDataRequestModel.Salt), error.Name);
        var message = Assert.Single(error.Value.EnumerateArray());
        Assert.Equal("Invalid master password salt.", message.GetString());

        Assert.False(await factory.GetDatabaseContext().Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower()));
    }

    /// <summary>
    /// Asserts that the account stores the expected salt and the expected account key shape, and that prelogin and the
    /// password login return the stored salt.
    /// </summary>
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
        using var loginBody = await AssertDefaultTokenBodyAsync(loginContext);
        var masterPasswordUnlock = AssertHelper.AssertJsonProperty(
            AssertHelper.AssertJsonProperty(loginBody.RootElement, "UserDecryptionOptions", JsonValueKind.Object),
            "MasterPasswordUnlock", JsonValueKind.Object);
        Assert.Equal(expectedSalt,
            AssertHelper.AssertJsonProperty(masterPasswordUnlock, "Salt", JsonValueKind.String).GetString());
    }
}

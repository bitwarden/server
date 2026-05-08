using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers;

public class AccountsControllerTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public AccountsControllerTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [BitAutoData("invalidEmail")]
    [BitAutoData("")]
    public async Task PostRegisterSendEmailVerification_InvalidRequestModel_ThrowsBadRequestException(string email, string name, bool receiveMarketingEmails)
    {

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_DisabledOpenRegistration_ThrowsBadRequestException(string name, bool receiveMarketingEmails)
    {

        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+register+{name}@email.com";
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }


    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task PostRegisterSendEmailVerification_WhenGivenNewOrExistingUser__WithEnableEmailVerificationTrue_ReturnsNoContent(bool shouldPreCreateUser, string name, bool receiveMarketingEmails)
    {
        var email = $"test+register+{name}@email.com";
        if (shouldPreCreateUser)
        {
            await CreateUserAsync(email, name);
        }

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }


    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task PostRegisterSendEmailVerification_WhenGivenNewOrExistingUser_WithEnableEmailVerificationFalse_ReturnsNoContent(bool shouldPreCreateUser, string name, bool receiveMarketingEmails)
    {

        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+register+{name}@email.com";
        if (shouldPreCreateUser)
        {
            await CreateUserAsync(email, name, localFactory);
        }

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        if (shouldPreCreateUser)
        {
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            var body = await context.ReadBodyAsStringAsync();
            Assert.Contains($"Email {email} is already taken", body);
        }
        else
        {
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            var body = await context.ReadBodyAsStringAsync();
            Assert.NotNull(body);
            Assert.StartsWith("BwRegistrationEmailVerificationToken_", body);
        }
    }

    [Theory, BitAutoData]
    // marketing emails can stay at top level
    public async Task RegistrationWithEmailVerification_WithEmailVerificationToken_Succeeds([Required] string name, bool receiveMarketingEmails,
         [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
         [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        // we must first call the send verification email endpoint to trigger the first part of the process
        var email = $"test+register+{name}@email.com";
        var sendVerificationEmailReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var sendEmailVerificationResponseHttpContext = await localFactory.PostRegisterSendEmailVerificationAsync(sendVerificationEmailReqModel);

        Assert.Equal(StatusCodes.Status204NoContent, sendEmailVerificationResponseHttpContext.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_OpenRegistrationDisabled_ThrowsBadRequestException([Required] string name, string emailVerificationToken,
       [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
       [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+register+{name}@email.com";

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgInviteToken_Succeeds(
         [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // To avoid having to call the API send org invite endpoint, I'm going to hardcode some valid org invite data:
        var email = "jsnider+local410@bitwarden.com";
        var orgInviteToken = "BwOrgUserInviteToken_CfDJ8HOzu6wr6nVLouuDxgOHsMwPcj9Guuip5k_XLD1bBGpwQS1f66c9kB6X4rvKGxNdywhgimzgvG9SgLwwJU70O8P879XyP94W6kSoT4N25a73kgW3nU3vl3fAtGSS52xdBjNU8o4sxmomRvhOZIQ0jwtVjdMC2IdybTbxwCZhvN0hKIFs265k6wFRSym1eu4NjjZ8pmnMneG0PlKnNZL93tDe8FMcqStJXoddIEgbA99VJp8z1LQmOMfEdoMEM7Zs8W5bZ34N4YEGu8XCrVau59kGtWQk7N4rPV5okzQbTpeoY_4FeywgLFGm-tDtTPEdSEBJkRjexANri7CGdg3dpnMifQc_bTmjZd32gOjw8N8v";
        var orgUserId = new Guid("5e45fbdc-a080-4a77-93ff-b19c0161e81e");

        var orgUser = new OrganizationUser { Id = orgUserId, Email = email, OrganizationId = Guid.NewGuid() };

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromHours(5))
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrgUserInviteTokenable>>(orgInviteTokenDataProtectorFactory =>
        {
            orgInviteTokenDataProtectorFactory.TryUnprotect(Arg.Is(orgInviteToken), out Arg.Any<OrgUserInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgInviteTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationUserRepository>(orgUserRepository =>
        {
            orgUserRepository.GetByIdAsync(orgUserId)
                .Returns(orgUser);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = orgUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgSponsoredFreeFamilyPlanInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, Guid orgSponsorshipId)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid org sponsored free family plan invite token data
        var email = "jsnider+local10000008@bitwarden.com";
        var orgSponsoredFreeFamilyPlanToken = "BWOrganizationSponsorship_CfDJ8HFsgwUNr89EtnCal5H72cx11wdMdD5_FSNMJoXJKp9migo8ZXi2Qx8GOM2b8IccesQEvZxzX_VDvhaaFi1NZc7-5bdadsfaPiwvzy28qwaW5-iF72vncmixArxKt8_FrJCqvn-5Yh45DvUWeOUBl1fPPx6LB4lgf6DcFkFZaHKOxIEywkFWEX9IWsLAfBfhU9K7AYZ02kxLRgXDK_eH3SKY0luoyUbRLBJRq1J9WnAQNcPLx9GOywQDUGRNvQGYmrzpAdq8y3MgUby_XD2NBf4-Vfr_0DIYPlGVJz0Ab1CwKbQ5G9vTXrFbbHQni40GVgohTq6WeVwk-PBMW9kjBw2rHO8QzWUb4whn831y-dEC";

        var orgSponsorship = new OrganizationSponsorship
        {
            Id = orgSponsorshipId,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            OfferedToEmail = email
        };

        var orgSponsorshipOfferTokenable = new OrganizationSponsorshipOfferTokenable(orgSponsorship) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(orgSponsoredFreeFamilyPlanToken), out Arg.Any<OrganizationSponsorshipOfferTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgSponsorshipOfferTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationSponsorshipRepository>(organizationSponsorshipRepository =>
        {
            organizationSponsorshipRepository.GetByIdAsync(orgSponsorshipId)
                .Returns(orgSponsorship);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgSponsoredFreeFamilyPlanToken = orgSponsoredFreeFamilyPlanToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithAcceptEmergencyAccessInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, EmergencyAccess emergencyAccess)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid data
        var email = "jsnider+local79813655659549@bitwarden.com";
        var acceptEmergencyAccessInviteToken = "CfDJ8HFsgwUNr89EtnCal5H72cwjvdjWmBp3J0ry7KoG6zDFub-EeoA3cfLBXONq7thKq7QTBh6KJ--jU0Det7t3P9EXqxmEacxIlgFlBgtywIUho9N8nVQeNcltkQO9g0vj_ASshnn6fWK3zpqS6Z8JueVZ2TMtdks5uc7DjZurWFLX27Dpii-UusFD78Z5tCY-D79bkjHy43g1ULk2F2ZtwiJvp3C9QvXW1-12IEsyHHSxU-9RELe-_joo2iDIR-cvMmEfbEXK7uvuzNT2V0r22jalaAKFvd84Gza9Q0YSFn8z_nAJxVqEXsAVKdG8SRN5Wa3K2mdNoBMt20RrzNuuJhe6vzX0yP35HtC4e1YXXzWB";
        var acceptEmergencyAccessId = new Guid("8bc5e574-cef6-4ee7-b9ed-b1e90158c016");

        emergencyAccess.Id = acceptEmergencyAccessId;
        emergencyAccess.Email = email;

        var emergencyAccessInviteTokenable = new EmergencyAccessInviteTokenable(emergencyAccess, 10) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(acceptEmergencyAccessInviteToken), out Arg.Any<EmergencyAccessInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = emergencyAccessInviteTokenable;
                    return true;
                });
        });


        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            AcceptEmergencyAccessInviteToken = acceptEmergencyAccessInviteToken,
            AcceptEmergencyAccessId = acceptEmergencyAccessId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithProviderInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid data
        var email = "jsnider+local253@bitwarden.com";
        var providerUserId = new Guid("c6fdba35-2e52-43b4-8fb7-b211011d154a");
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {email} {nowMillis}";
        // var providerInviteToken = await GetValidProviderInviteToken(localFactory, email, providerUserId);

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        localFactory.SubstituteService<IDataProtectionProvider>(dataProtectionProvider =>
        {
            dataProtectionProvider.CreateProtector(Arg.Any<string>())
                .Returns(mockDataProtector);
        });

        // As token contains now milliseconds for when it was created, create 1k year timespan for expiration
        // to ensure token is valid for a good long while.
        localFactory.UpdateConfiguration("globalSettings:OrganizationInviteExpirationHours", "8760000");

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            ProviderInviteToken = base64EncodedProviderInvToken,
            ProviderUserId = providerUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_Success(
        [Required, StringLength(20)] string name,
        string emailVerificationToken)
    {
        // Arrange
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        var email = $"test+register+{name}@email.com";
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email);

        localFactory.SubstituteService<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>(emailVerificationTokenDataProtectorFactory =>
        {
            emailVerificationTokenDataProtectorFactory.TryUnprotect(Arg.Is(emailVerificationToken), out Arg.Any<RegistrationEmailVerificationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = registrationEmailVerificationTokenable;
                    return true;
                });
        });

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act
        var httpContext = await localFactory.PostRegisterVerificationEmailClicked(requestModel);

        var body = await httpContext.ReadBodyAsStringAsync();

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    private async Task<User> CreateUserAsync(string email, string name, IdentityApplicationFactory factory = null, string masterPasswordSalt = null)
    {
        var factoryToUse = factory ?? _factory;

        var userRepository = factoryToUse.Services.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Email = email,
            Id = Guid.NewGuid(),
            Name = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test_api_key",
            MasterPasswordSalt = masterPasswordSalt,
        };

        await userRepository.CreateAsync(user);

        return user;
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithSalt_ReturnsStoredSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: email);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithNullSalt_ReturnsNullSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: null);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("salt").ValueKind);
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndDefaultHashKeyConfigured_ReturnsDeterministicResult([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var email = $"nonexistent+prelogin+{name}@email.com";

        var first = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });
        var second = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        using var firstBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(first);
        using var secondBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(second);
        Assert.Equal(firstBody.RootElement.GetProperty("salt").GetRawText(), secondBody.RootElement.GetProperty("salt").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdf").GetRawText(), secondBody.RootElement.GetProperty("kdf").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdfIterations").GetRawText(), secondBody.RootElement.GetProperty("kdfIterations").GetRawText());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndNoDefaultHashKey_ReturnsEmailAsSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", null);
        var email = $"nonexistent+prelogin+{name}@email.com";

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExist_ReturnsSaltIndependentOfInputCasing([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var lowercaseEmail = $"nonexistent+prelogin+{name}@email.com";
        var mixedCaseEmail = lowercaseEmail.ToUpperInvariant();

        var lowercase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = lowercaseEmail });
        var mixedCase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = mixedCaseEmail });

        Assert.Equal(StatusCodes.Status200OK, lowercase.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, mixedCase.Response.StatusCode);
        using var lowercaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(lowercase);
        using var mixedCaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(mixedCase);
        Assert.Equal(lowercaseBody.RootElement.GetProperty("salt").GetRawText(), mixedCaseBody.RootElement.GetProperty("salt").GetRawText());
    }

}

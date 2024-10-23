using System.ComponentModel.DataAnnotations;
using System.Text;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
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

    [Fact]
    public async Task PostRegister_Success()
    {
        var context = await _factory.RegisterAsync(new RegisterRequestModel
        {
            Email = "test+register@email.com",
            MasterPasswordHash = "master_password_hash"
        });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var database = _factory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == "test+register@email.com");

        Assert.NotNull(user);
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
    public async Task RegistrationWithEmailVerification_WithEmailVerificationToken_Succeeds([Required] string name, bool receiveMarketingEmails,
         [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
         [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        // First we must substitute the mail service in order to be able to get a valid email verification token
        // for the complete registration step
        string capturedEmailVerificationToken = null;
        localFactory.SubstituteService<IMailService>(mailService =>
        {
            mailService.SendRegistrationVerificationEmailAsync(Arg.Any<string>(), Arg.Do<string>(t => capturedEmailVerificationToken = t))
                .Returns(Task.CompletedTask);

        });

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
        Assert.NotNull(capturedEmailVerificationToken);

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = capturedEmailVerificationToken,
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

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // To avoid having to call the API send org invite endpoint, I'm going to hardcode some valid org invite data:
        var email = "jsnider+local410@bitwarden.com";
        var orgInviteToken = "BwOrgUserInviteToken_CfDJ8HOzu6wr6nVLouuDxgOHsMwPcj9Guuip5k_XLD1bBGpwQS1f66c9kB6X4rvKGxNdywhgimzgvG9SgLwwJU70O8P879XyP94W6kSoT4N25a73kgW3nU3vl3fAtGSS52xdBjNU8o4sxmomRvhOZIQ0jwtVjdMC2IdybTbxwCZhvN0hKIFs265k6wFRSym1eu4NjjZ8pmnMneG0PlKnNZL93tDe8FMcqStJXoddIEgbA99VJp8z1LQmOMfEdoMEM7Zs8W5bZ34N4YEGu8XCrVau59kGtWQk7N4rPV5okzQbTpeoY_4FeywgLFGm-tDtTPEdSEBJkRjexANri7CGdg3dpnMifQc_bTmjZd32gOjw8N8v";
        var orgUserId = new Guid("5e45fbdc-a080-4a77-93ff-b19c0161e81e");

        var orgUser = new OrganizationUser { Id = orgUserId, Email = email };

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

    private async Task<User> CreateUserAsync(string email, string name, IdentityApplicationFactory factory = null)
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
        };

        await userRepository.CreateAsync(user);

        return user;
    }

}
